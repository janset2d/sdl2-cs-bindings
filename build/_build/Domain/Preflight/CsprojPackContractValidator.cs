using System.Globalization;
using System.Xml.Linq;
using Build.Context.Models;
using Build.Domain.Preflight.Models;
using Build.Domain.Preflight.Results;
using Cake.Core.IO;

namespace Build.Domain.Preflight;

/// <summary>
/// Validates that every managed and native csproj referenced by <c>manifest.json package_families[]</c>
/// conforms to the canonical pack contract documented in
/// <c>docs/knowledge-base/release-guardrails.md</c> guardrails G4, G6, G7, G17, G18.
/// </summary>
/// <remarks>
/// Post-S1 scope (2026-04-17): guardrails G1 (PrivateAssets="all"), G2 (paired PackageReference),
/// G3 (bracket-notation PackageVersion), G5 (family-version property name convention), and G8
/// (sentinel fallback) were retired when within-family exact-pin was replaced with SkiaSharp-style
/// minimum range. This validator retains the structural checks that remain relevant post-S1:
/// canonical PackageId naming (G6), MinVerTagPrefix alignment with manifest (G4), Native
/// ProjectReference path correctness (G7), and manifest cross-section references (G17, G18).
///
/// The validator parses each csproj as XML (no MSBuild evaluation) so it can run inside PreFlight
/// without invoking the SDK. This deliberately makes it a structural check — it cannot evaluate
/// expressions like <c>$(Version)</c>, only verify their literal presence.
/// </remarks>
public sealed class CsprojPackContractValidator(IFileSystem fileSystem) : ICsprojPackContractValidator
{
    private readonly IFileSystem _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));

    public CsprojPackContractResult Validate(ManifestConfig manifest, DirectoryPath repoRoot)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(repoRoot);

        var checks = new List<CsprojPackContractCheck>();

        var familyIdentifiers = manifest.PackageFamilies
            .Select(f => f.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var libraryNames = manifest.LibraryManifests
            .Select(l => l.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var family in manifest.PackageFamilies)
        {
            ValidateDependsOn(family, familyIdentifiers, checks);
            ValidateLibraryRef(family, libraryNames, checks);

            if (string.IsNullOrEmpty(family.ManagedProject) || string.IsNullOrEmpty(family.NativeProject))
            {
                continue;
            }

            ValidateManagedCsproj(family, repoRoot, checks);
            ValidateNativeCsproj(family, repoRoot, checks);
        }

        var validation = new CsprojPackContractValidation(checks);

        return validation.HasErrors
            ? CsprojPackContractResult.Fail(validation)
            : CsprojPackContractResult.Pass(validation);
    }

    private void ValidateManagedCsproj(PackageFamilyConfig family, DirectoryPath repoRoot, List<CsprojPackContractCheck> checks)
    {
        var relativePath = family.ManagedProject!;
        var (root, error) = LoadCsprojDocument(family, repoRoot, relativePath);
        if (root is null)
        {
            checks.Add(error!);
            return;
        }

        checks.Add(CheckManagedPackageId(family, relativePath, root));
        checks.Add(CheckMinVerTagPrefix(family, relativePath, root));
        checks.Add(CheckNativeProjectReferencePath(family, repoRoot, relativePath, root));
    }

    private void ValidateNativeCsproj(PackageFamilyConfig family, DirectoryPath repoRoot, List<CsprojPackContractCheck> checks)
    {
        var relativePath = family.NativeProject!;
        var (root, error) = LoadCsprojDocument(family, repoRoot, relativePath);
        if (root is null)
        {
            checks.Add(error!);
            return;
        }

        var actualPackageId = ReadPropertyValue(root, "PackageId");
        var expectedNativePackageId = FamilyIdentifierConventions.NativePackageId(family.Name);
        checks.Add(new CsprojPackContractCheck(
            family.Name,
            relativePath,
            CsprojPackContractCheckKind.PackageIdMatchesCanonicalConvention,
            IsValid: string.Equals(actualPackageId, expectedNativePackageId, StringComparison.Ordinal),
            ExpectedValue: expectedNativePackageId,
            ActualValue: actualPackageId,
            ErrorMessage: string.Equals(actualPackageId, expectedNativePackageId, StringComparison.Ordinal)
                ? null
                : $"Native csproj <PackageId> must equal '{expectedNativePackageId}' (canonical Janset.SDL<Major>.<Role>.Native). Actual: '{actualPackageId ?? "<missing>"}'."));

        var actualMinVerTagPrefix = ReadPropertyValue(root, "MinVerTagPrefix");
        var expectedMinVerTagPrefix = FamilyIdentifierConventions.MinVerTagPrefix(family.TagPrefix);
        checks.Add(new CsprojPackContractCheck(
            family.Name,
            relativePath,
            CsprojPackContractCheckKind.MinVerTagPrefixMatchesManifest,
            IsValid: string.Equals(actualMinVerTagPrefix, expectedMinVerTagPrefix, StringComparison.Ordinal),
            ExpectedValue: expectedMinVerTagPrefix,
            ActualValue: actualMinVerTagPrefix,
            ErrorMessage: string.Equals(actualMinVerTagPrefix, expectedMinVerTagPrefix, StringComparison.Ordinal)
                ? null
                : $"Native csproj <MinVerTagPrefix> must equal '{expectedMinVerTagPrefix}' (manifest tag_prefix + '-'). Actual: '{actualMinVerTagPrefix ?? "<missing>"}'."));
    }

    // Synchronous file read justified: PreFlight runs once per Cake invocation against ~10 small csproj
    // files. Switching the validator surface to async would propagate Task<...> through ICsprojPack-
    // ContractValidator, IPreflightReporter, PreFlightCheckTask without measurable benefit. MA0045
    // suppression here mirrors the rationale in Program.cs file-level pragma.
#pragma warning disable MA0045
    private (XElement? Root, CsprojPackContractCheck? Error) LoadCsprojDocument(PackageFamilyConfig family, DirectoryPath repoRoot, string relativePath)
    {
        var fullPath = repoRoot.CombineWithFilePath(relativePath);
        var file = _fileSystem.GetFile(fullPath);

        if (!file.Exists)
        {
            return (null, new CsprojPackContractCheck(
                family.Name,
                relativePath,
                CsprojPackContractCheckKind.CsprojFileExists,
                IsValid: false,
                ExpectedValue: relativePath,
                ActualValue: "<missing>",
                ErrorMessage: $"Csproj declared by package_families[] not found at '{relativePath}'."));
        }

        try
        {
            using var stream = file.OpenRead();
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            buffer.Position = 0;
            var document = XDocument.Load(buffer);
            return (document.Root, null);
        }
        catch (Exception ex) when (ex is System.Xml.XmlException or IOException)
        {
            return (null, new CsprojPackContractCheck(
                family.Name,
                relativePath,
                CsprojPackContractCheckKind.CsprojFileExists,
                IsValid: false,
                ExpectedValue: "<parseable XML>",
                ActualValue: ex.Message,
                ErrorMessage: $"Failed to parse csproj '{relativePath}': {ex.Message}"));
        }
    }
#pragma warning restore MA0045

    private static CsprojPackContractCheck CheckManagedPackageId(PackageFamilyConfig family, string relativePath, XElement root)
    {
        var actual = ReadPropertyValue(root, "PackageId");
        var expected = FamilyIdentifierConventions.ManagedPackageId(family.Name);
        return new CsprojPackContractCheck(
            family.Name,
            relativePath,
            CsprojPackContractCheckKind.PackageIdMatchesCanonicalConvention,
            IsValid: string.Equals(actual, expected, StringComparison.Ordinal),
            ExpectedValue: expected,
            ActualValue: actual,
            ErrorMessage: string.Equals(actual, expected, StringComparison.Ordinal)
                ? null
                : $"Managed csproj <PackageId> must equal '{expected}' (canonical Janset.SDL<Major>.<Role>). Actual: '{actual ?? "<missing>"}'.");
    }

    private static CsprojPackContractCheck CheckMinVerTagPrefix(PackageFamilyConfig family, string relativePath, XElement root)
    {
        var actual = ReadPropertyValue(root, "MinVerTagPrefix");
        var expected = FamilyIdentifierConventions.MinVerTagPrefix(family.TagPrefix);
        return new CsprojPackContractCheck(
            family.Name,
            relativePath,
            CsprojPackContractCheckKind.MinVerTagPrefixMatchesManifest,
            IsValid: string.Equals(actual, expected, StringComparison.Ordinal),
            ExpectedValue: expected,
            ActualValue: actual,
            ErrorMessage: string.Equals(actual, expected
                , StringComparison.Ordinal)
                ? null
                : $"Managed csproj <MinVerTagPrefix> must equal '{expected}' (manifest tag_prefix + '-'). Actual: '{actual ?? "<missing>"}'.");
    }

    private static CsprojPackContractCheck CheckNativeProjectReferencePath(PackageFamilyConfig family, DirectoryPath repoRoot, string relativePath, XElement root)
    {
        var managedFullPath = repoRoot.CombineWithFilePath(relativePath);
        var managedDir = managedFullPath.GetDirectory();
        var nativeAbsolute = repoRoot.CombineWithFilePath(family.NativeProject!).Collapse().FullPath;

        var matching = root
            .Descendants("ProjectReference")
            .Where(pr =>
            {
                var include = pr.Attribute("Include")?.Value ?? string.Empty;
                var resolved = managedDir.CombineWithFilePath(include.Replace('\\', '/')).Collapse();
                return PathsEqual(resolved.FullPath, nativeAbsolute);
            })
            .ToList();

        var expectedRelative = managedDir.GetRelativePath(repoRoot.CombineWithFilePath(family.NativeProject!)).FullPath;
        return new CsprojPackContractCheck(
            family.Name,
            relativePath,
            CsprojPackContractCheckKind.NativeProjectReferencePathMatchesManifest,
            IsValid: matching.Count == 1,
            ExpectedValue: expectedRelative,
            ActualValue: matching.Count.ToString(CultureInfo.InvariantCulture) + " match(es)",
            ErrorMessage: matching.Count == 1
                ? null
                : $"Expected exactly one Native <ProjectReference> resolving to '{family.NativeProject}'. Found {matching.Count}.");
    }

    private static void ValidateDependsOn(PackageFamilyConfig family, HashSet<string> familyIdentifiers, List<CsprojPackContractCheck> checks)
    {
        foreach (var dep in family.DependsOn)
        {
            var exists = familyIdentifiers.Contains(dep);
            checks.Add(new CsprojPackContractCheck(
                family.Name,
                CsprojRelativePath: string.Empty,
                CsprojPackContractCheckKind.DependsOnReferencesExistingFamily,
                IsValid: exists,
                ExpectedValue: $"package_families[].name == '{dep}'",
                ActualValue: exists ? dep : "<not found>",
                ErrorMessage: exists
                    ? null
                    : $"Family '{family.Name}' depends_on references unknown family '{dep}'. Add it to package_families[] or fix the typo."));
        }
    }

    private static void ValidateLibraryRef(PackageFamilyConfig family, HashSet<string> libraryNames, List<CsprojPackContractCheck> checks)
    {
        var exists = libraryNames.Contains(family.LibraryRef);
        checks.Add(new CsprojPackContractCheck(
            family.Name,
            CsprojRelativePath: string.Empty,
            CsprojPackContractCheckKind.LibraryRefReferencesExistingLibrary,
            IsValid: exists,
            ExpectedValue: $"library_manifests[].name == '{family.LibraryRef}'",
            ActualValue: exists ? family.LibraryRef : "<not found>",
            ErrorMessage: exists
                ? null
                : $"Family '{family.Name}' library_ref references unknown library '{family.LibraryRef}'. Add it to library_manifests[] or fix the typo."));
    }

    private static string? ReadPropertyValue(XElement root, string propertyName)
    {
        return root
            .Descendants("PropertyGroup")
            .SelectMany(pg => pg.Elements(propertyName))
            .Select(e => e.Value?.Trim())
            .FirstOrDefault(v => !string.IsNullOrEmpty(v));
    }

    private static bool PathsEqual(string a, string b)
    {
        var normalizedA = a.Replace('\\', '/').TrimEnd('/');
        var normalizedB = b.Replace('\\', '/').TrimEnd('/');
        return string.Equals(normalizedA, normalizedB, StringComparison.OrdinalIgnoreCase);
    }
}
