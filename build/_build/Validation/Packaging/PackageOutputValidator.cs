using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Xml.Linq;
using Build.Results;
using Build.Targets.Package.Models;
using Build.Shared.Manifest;
using Build.Shared.Packaging;
using Build.Validation.Conventions;
using Cake.Core.IO;
using NuGet.Frameworks;
using NuGet.Versioning;

namespace Build.Validation.Packaging;

/// <summary>
/// Post-pack nuspec assertions for a packed family (one managed + one native .nupkg).
/// </summary>
/// <remarks>
/// Guardrails G20 (within-family exact-pin `[x.y.z]` assertion)
/// and G24 (sentinel leak check) were retired when within-family dependencies moved to
/// SkiaSharp-style minimum range. The validator now enforces:
/// <list type="bullet">
///   <item><description>G21 — within-family native dependency emits bare minimum range `x.y.z`; cross-family dependencies preserve lower bound `&gt;= x.y.z`.</description></item>
///   <item><description>G22 — all TFM dependency groups agree.</description></item>
///   <item><description>G23 — managed and native packages emit identical <c>&lt;version&gt;</c> elements (primary within-family coherence check).</description></item>
///   <item><description>G25 — managed symbol package (.snupkg) is present and valid.</description></item>
///   <item><description>G26 — nuspec <c>&lt;repository&gt;</c> commit matches expected SHA.</description></item>
///   <item><description>G27 — nuspec metadata (id, authors, license, icon) matches project metadata.</description></item>
///   <item><description>G47 — native package ships the consumer-side buildTransitive contract (thin wrapper + shared common.targets).</description></item>
///   <item><description>G48 — every <c>runtimes/&lt;rid&gt;/native/</c> subtree in the native package has the correct payload shape (DLLs on Windows, <c>$(PackageId).tar.gz</c> on Unix).</description></item>
///   <item><description>G55 — native package ships valid root <c>janset-native-metadata.json</c> matching manifest/build invariants.</description></item>
///   <item><description>G56 — every satellite cross-family dependency declares upper bound <c>&lt; (UpstreamMajor + 1).0.0</c>.</description></item>
///   <item><description>G57 — README mapping block between JANSET markers is current and generator-equivalent.</description></item>
/// </list>
///
/// Every guardrail is evaluated and added to the returned <see cref="ValidationReport"/>.
/// Operators see the full failure set instead of the first-throw-wins subset produced by the
/// pre-Result-pattern surface.
/// </remarks>
public sealed class PackageOutputValidator(
    IFileSystem fileSystem,
    INativePackageMetadataValidator nativePackageMetadataValidator,
    IReadmeMappingTableValidator readmeMappingTableValidator) : IPackageOutputValidator
{
    private readonly IFileSystem _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    private readonly INativePackageMetadataValidator _nativePackageMetadataValidator = nativePackageMetadataValidator ?? throw new ArgumentNullException(nameof(nativePackageMetadataValidator));
    private readonly IReadmeMappingTableValidator _readmeMappingTableValidator = readmeMappingTableValidator ?? throw new ArgumentNullException(nameof(readmeMappingTableValidator));

    public async Task<ValidationReport> ValidateAsync(
        PackageFamilyConfig family,
        PackageArtifacts artifacts,
        string expectedVersion,
        string expectedCommitSha,
        ProjectMetadata managedProjectMetadata,
        ManifestConfig manifestConfig,
        FilePath readmePath)
    {
        ArgumentNullException.ThrowIfNull(family);
        ArgumentNullException.ThrowIfNull(artifacts);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedCommitSha);
        ArgumentNullException.ThrowIfNull(managedProjectMetadata);
        ArgumentNullException.ThrowIfNull(manifestConfig);
        ArgumentNullException.ThrowIfNull(readmePath);

        var checks = new List<ValidationCheck>();

        AddProjectMetadataCompletenessChecks(checks, family, managedProjectMetadata);

        var managedNuspec = await TryLoadNuspecAsync(checks, artifacts.ManagedPackage, "managed package");
        var nativeNuspec = await TryLoadNuspecAsync(checks, artifacts.NativePackage, "native package");

        var managedMetadata = TryGetMetadataRoot(checks, artifacts.ManagedPackage, managedNuspec);
        var nativeMetadata = TryGetMetadataRoot(checks, artifacts.NativePackage, nativeNuspec);

        if (managedMetadata is not null)
        {
            EvaluateCanonicalMetadata(
                checks,
                artifacts.ManagedPackage,
                managedMetadata,
                FamilyIdentifierConventions.ManagedPackageId(family.Name),
                expectedVersion,
                expectedCommitSha,
                managedProjectMetadata);

            EvaluateDependencyGroups(
                checks,
                family,
                artifacts.ManagedPackage,
                managedMetadata,
                expectedVersion,
                managedProjectMetadata,
                manifestConfig);
        }

        if (nativeMetadata is not null)
        {
            EvaluateCanonicalMetadata(
                checks,
                artifacts.NativePackage,
                nativeMetadata,
                FamilyIdentifierConventions.NativePackageId(family.Name),
                expectedVersion,
                expectedCommitSha,
                managedProjectMetadata);
        }

        if (managedMetadata is not null && nativeMetadata is not null)
        {
            EvaluateWithinFamilyVersionCoherence(checks, managedMetadata, nativeMetadata, artifacts.ManagedPackage, artifacts.NativePackage);
        }

        await EvaluateManagedSymbolsAsync(checks, artifacts.ManagedSymbolsPackage);
        await EvaluateNativePackageLayoutAsync(checks, family, artifacts.NativePackage);
        AddIfPresent(checks, await _nativePackageMetadataValidator.ValidateAsync(family, artifacts.NativePackage, expectedVersion, expectedCommitSha, manifestConfig));
        AddIfPresent(checks, _readmeMappingTableValidator.Validate(family, readmePath, manifestConfig));

        return new ValidationReport(checks);
    }

    /// <summary>
    /// Emits a <see cref="ValidationCheck"/> only when <paramref name="isValid"/> is false.
    /// </summary>
    private static void AddCheck(
        List<ValidationCheck> checks,
        bool isValid,
        string? code,
        string name,
        string message)
    {
        if (!isValid)
        {
            checks.Add(new ValidationCheck(name, ValidationSeverity.Error, message, code));
        }
    }

    private static void AddIfPresent(List<ValidationCheck> checks, ValidationCheck? check)
    {
        if (check is not null)
        {
            checks.Add(check);
        }
    }

    private static void AddProjectMetadataCompletenessChecks(
        List<ValidationCheck> checks,
        PackageFamilyConfig family,
        ProjectMetadata metadata)
    {
        AddCompletenessCheck(
            checks,
            metadata.TargetFrameworks.Count != 0,
            $"ProjectMetadata for family '{family.Name}' has no target frameworks. Check that the managed csproj declares <TargetFrameworks> (directly or via Directory.Build.props).");

        AddCompletenessCheck(
            checks,
            !string.IsNullOrWhiteSpace(metadata.Authors),
            $"ProjectMetadata for family '{family.Name}' is missing Authors. Expected value from Directory.Build.props.");

        AddCompletenessCheck(
            checks,
            !string.IsNullOrWhiteSpace(metadata.PackageLicenseFile),
            $"ProjectMetadata for family '{family.Name}' is missing PackageLicenseFile. Expected value from Directory.Build.props.");

        AddCompletenessCheck(
            checks,
            !string.IsNullOrWhiteSpace(metadata.PackageIcon),
            $"ProjectMetadata for family '{family.Name}' is missing PackageIcon. Expected value from Directory.Build.props.");
    }

    private static void AddCompletenessCheck(
        List<ValidationCheck> checks,
        bool isValid,
        string message)
        => AddCheck(checks, isValid, code: null, name: "Project metadata completeness", message);

    private async Task<PackageNuspec?> TryLoadNuspecAsync(
        List<ValidationCheck> checks,
        FilePath packagePath,
        string description)
    {
        var file = _fileSystem.GetFile(packagePath);
        if (!file.Exists)
        {
            AddCheck(checks, isValid: false, code: null, name: "Nuspec load",
                message: $"Post-pack assertion failed: expected {description} '{packagePath.GetFilename().FullPath}' was not produced.");
            return null;
        }

        try
        {
            await using var stream = file.OpenRead();
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);

            var nuspecEntry = archive.Entries.SingleOrDefault(entry => entry.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase));
            if (nuspecEntry is null)
            {
                AddCheck(checks, isValid: false, code: null, name: "Nuspec load",
                    message: $"Post-pack assertion failed: package '{packagePath.GetFilename().FullPath}' does not contain a .nuspec entry.");
                return null;
            }

#pragma warning disable CA1849, S6966 // ZipArchiveEntry.Open sync used intentionally for small metadata reads
            using var reader = new StreamReader(nuspecEntry.Open());
#pragma warning restore CA1849, S6966
            var rawXml = await reader.ReadToEndAsync();
            var document = XDocument.Parse(rawXml, LoadOptions.PreserveWhitespace);

            return new PackageNuspec(document, rawXml);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or System.Xml.XmlException)
        {
            AddCheck(checks, isValid: false, code: null, name: "Nuspec load",
                message: $"Post-pack assertion failed: could not read nuspec in '{packagePath.GetFilename().FullPath}': {ex.Message}");
            return null;
        }
    }

    private static XElement? TryGetMetadataRoot(
        List<ValidationCheck> checks,
        FilePath packagePath,
        PackageNuspec? nuspec)
    {
        if (nuspec is null)
        {
            return null;
        }

        var metadata = nuspec.Document.Root?.Elements().SingleOrDefault(element => string.Equals(element.Name.LocalName, "metadata", StringComparison.Ordinal));
        if (metadata is null)
        {
            AddCheck(checks, isValid: false, code: null, name: "Nuspec load",
                message: $"Post-pack assertion failed: package '{packagePath.GetFilename().FullPath}' is missing required element 'metadata'.");
        }

        return metadata;
    }

    [SuppressMessage("Design", "MA0051:Method is too long",
        Justification = "Six sequential canonical checks kept co-located for readability; splitting hurts traceability to guardrails G26/G27.")]
    private static void EvaluateCanonicalMetadata(
        List<ValidationCheck> checks,
        FilePath packagePath,
        XElement metadata,
        string expectedPackageId,
        string expectedVersion,
        string expectedCommitSha,
        ProjectMetadata projectMetadata)
    {
        var packageId = TryGetChildValue(metadata, "id", out var missingId);
        AddCanonicalCheck(
            checks,
            isValid: !missingId && string.Equals(packageId, expectedPackageId, StringComparison.Ordinal),
            $"G27: package '{packagePath.GetFilename().FullPath}' emitted id '{packageId ?? "<missing>"}', expected '{expectedPackageId}'.");

        var version = TryGetChildValue(metadata, "version", out var missingVersion);
        AddCanonicalCheck(
            checks,
            isValid: !missingVersion && string.Equals(version, expectedVersion, StringComparison.Ordinal),
            $"G23/G27: package '{packagePath.GetFilename().FullPath}' emitted version '{version ?? "<missing>"}', expected '{expectedVersion}'.");

        var authors = TryGetChildValue(metadata, "authors", out var missingAuthors);
        AddCanonicalCheck(
            checks,
            isValid: !missingAuthors && string.Equals(authors, projectMetadata.Authors, StringComparison.Ordinal),
            $"G27: package '{packagePath.GetFilename().FullPath}' emitted authors '{authors ?? "<missing>"}', expected '{projectMetadata.Authors}' (resolved from Directory.Build.props).");

        var license = metadata.Elements().SingleOrDefault(element => string.Equals(element.Name.LocalName, "license", StringComparison.Ordinal));
        if (license is null)
        {
            AddCanonicalCheck(
                checks,
                isValid: false,
                $"G27: package '{packagePath.GetFilename().FullPath}' is missing required element 'license'.");
        }
        else
        {
            var licenseType = license.Attributes().SingleOrDefault(attr => string.Equals(attr.Name.LocalName, "type", StringComparison.Ordinal))?.Value?.Trim();
            var licenseValue = license.Value.Trim();

            var licenseValid = string.Equals(licenseType, "file", StringComparison.OrdinalIgnoreCase) &&
                               string.Equals(licenseValue, projectMetadata.PackageLicenseFile, StringComparison.Ordinal);

            AddCanonicalCheck(
                checks,
                isValid: licenseValid,
                $"G27: package '{packagePath.GetFilename().FullPath}' emitted license '{licenseValue}' type '{licenseType ?? "<missing>"}', expected file '{projectMetadata.PackageLicenseFile}'.");
        }

        var icon = TryGetChildValue(metadata, "icon", out var missingIcon);
        AddCanonicalCheck(
            checks,
            isValid: !missingIcon && string.Equals(icon, projectMetadata.PackageIcon, StringComparison.Ordinal),
            $"G27: package '{packagePath.GetFilename().FullPath}' emitted icon '{icon ?? "<missing>"}', expected '{projectMetadata.PackageIcon}'.");

        var repository = metadata.Elements().SingleOrDefault(element => string.Equals(element.Name.LocalName, "repository", StringComparison.Ordinal));
        if (repository is null)
        {
            AddCanonicalCheck(
                checks,
                isValid: false,
                $"G26: package '{packagePath.GetFilename().FullPath}' is missing required element 'repository'.",
                code: "G26",
                name: "Repository commit");
        }
        else
        {
            var actualCommit = repository.Attributes().SingleOrDefault(attr => string.Equals(attr.Name.LocalName, "commit", StringComparison.Ordinal))?.Value?.Trim() ??
                               string.Empty;
            var commitValid = string.Equals(actualCommit, expectedCommitSha, StringComparison.OrdinalIgnoreCase);
            AddCanonicalCheck(
                checks,
                isValid: commitValid,
                $"G26: package '{packagePath.GetFilename().FullPath}' emitted repository commit '{actualCommit}', expected '{expectedCommitSha}'.",
                code: "G26",
                name: "Repository commit");
        }
    }

    private static void AddCanonicalCheck(
        List<ValidationCheck> checks,
        bool isValid,
        string message,
        string code = "G27",
        string name = "Nuspec metadata")
        => AddCheck(checks, isValid, code, name, message);

    [SuppressMessage("Design", "MA0051:Method is too long",
        Justification =
            "Dependency-group walking interleaves framework parity, inter-group consistency, and per-group expected-dependency checks (G21/G22) — splitting them obscures the per-framework control flow.")]
    private static void EvaluateDependencyGroups(
        List<ValidationCheck> checks,
        PackageFamilyConfig family,
        FilePath managedPackagePath,
        XElement metadata,
        string expectedVersion,
        ProjectMetadata projectMetadata,
        ManifestConfig manifestConfig)
    {
        var dependencies = metadata.Elements().SingleOrDefault(element => string.Equals(element.Name.LocalName, "dependencies", StringComparison.Ordinal));
        if (dependencies is null)
        {
            AddCheck(checks, isValid: false, code: "G22", name: "TFM agreement",
                message: $"G21-G22: managed package '{managedPackagePath.GetFilename().FullPath}' is missing a <dependencies> section.");
            return;
        }

        var groups = dependencies.Elements()
            .Where(element => string.Equals(element.Name.LocalName, "group", StringComparison.Ordinal))
            .ToList();

        if (groups.Count == 0)
        {
            AddCheck(checks, isValid: false, code: "G22", name: "TFM agreement",
                message: $"G21-G22: managed package '{managedPackagePath.GetFilename().FullPath}' emitted no dependency groups.");
            return;
        }

        var expectedFrameworks = new HashSet<NuGetFramework>();
        foreach (var raw in projectMetadata.TargetFrameworks)
        {
            var parsed = NuGetFramework.ParseFolder(raw);
            if (parsed.IsUnsupported)
            {
                AddCheck(checks, isValid: false, code: "G22", name: "TFM agreement",
                    message:
                    $"G22: managed package '{managedPackagePath.GetFilename().FullPath}' has a non-parseable expected target framework '{raw}' resolved from the csproj.");
                return;
            }

            expectedFrameworks.Add(parsed);
        }

        var actualFrameworksByKey = new List<(XElement Group, NuGetFramework Framework)>();
        foreach (var group in groups)
        {
            var tfmAttr = group.Attributes().SingleOrDefault(attr => string.Equals(attr.Name.LocalName, "targetFramework", StringComparison.Ordinal))?.Value?.Trim();
            if (string.IsNullOrWhiteSpace(tfmAttr))
            {
                AddCheck(checks, isValid: false, code: "G22", name: "TFM agreement",
                    message:
                    $"G22: managed package '{managedPackagePath.GetFilename().FullPath}' has a dependency group missing required attribute 'group/@targetFramework'.");
                return;
            }

            var parsedFramework = NuGetFramework.Parse(tfmAttr);
            if (parsedFramework.IsUnsupported)
            {
                AddCheck(checks, isValid: false, code: "G22", name: "TFM agreement",
                    message:
                    $"G22: managed package '{managedPackagePath.GetFilename().FullPath}' emitted dependency group with non-parseable targetFramework '{tfmAttr}'.");
                return;
            }

            actualFrameworksByKey.Add((group, parsedFramework));
        }

        var actualFrameworks = actualFrameworksByKey.Select(item => item.Framework).ToHashSet();

        var frameworksMatch = actualFrameworks.SetEquals(expectedFrameworks);
        if (!frameworksMatch)
        {
            var expectedLabel = string.Join(", ", expectedFrameworks.Select(FormatFramework).OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
            var actualLabel = string.Join(", ", actualFrameworks.Select(FormatFramework).OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
            AddCheck(checks, isValid: false, code: "G22", name: "TFM agreement",
                message:
                $"G22: managed package '{managedPackagePath.GetFilename().FullPath}' emitted framework groups '{actualLabel}', expected '{expectedLabel}' (resolved from csproj).");
        }

        Dictionary<string, DependencyContract>? baselineContracts = null;
        var interGroupInconsistent = false;

        foreach (var (group, framework) in actualFrameworksByKey)
        {
            var rawDependencies = group.Elements()
                .Where(element => string.Equals(element.Name.LocalName, "dependency", StringComparison.Ordinal))
                .Select(element => new
                {
                    Id = element.Attributes().SingleOrDefault(attr => string.Equals(attr.Name.LocalName, "id", StringComparison.Ordinal))?.Value?.Trim(),
                    Version = element.Attributes().SingleOrDefault(attr => string.Equals(attr.Name.LocalName, "version", StringComparison.Ordinal))?.Value?.Trim(),
                    Include = element.Attributes().SingleOrDefault(attr => string.Equals(attr.Name.LocalName, "include", StringComparison.Ordinal))?.Value?.Trim(),
                    Exclude = element.Attributes().SingleOrDefault(attr => string.Equals(attr.Name.LocalName, "exclude", StringComparison.Ordinal))?.Value?.Trim(),
                })
                .Where(dep => !string.IsNullOrWhiteSpace(dep.Id) && !string.IsNullOrWhiteSpace(dep.Version))
                .ToList();

            // Detect duplicate dependency IDs within a single group. dotnet pack should never
            // emit them, but a malformed nuspec must surface as a guardrail finding rather
            // than an unhandled ArgumentException from ToDictionary.
            var duplicateIds = rawDependencies
                .GroupBy(dep => dep.Id!, StringComparer.OrdinalIgnoreCase)
                .Where(idGroup => idGroup.Count() > 1)
                .Select(idGroup => idGroup.Key)
                .ToList();

            if (duplicateIds.Count > 0)
            {
                AddCheck(checks, isValid: false, code: "G22", name: "TFM agreement",
                    message:
                    $"G22: managed package '{managedPackagePath.GetFilename().FullPath}' emitted duplicate dependency id(s) '{string.Join(", ", duplicateIds)}' " +
                    $"in dependency group '{FormatFramework(framework)}'.");
                return;
            }

            var dependencyContracts = rawDependencies.ToDictionary(
                dep => dep.Id!,
                dep => new DependencyContract(dep.Version!, NullIfEmpty(dep.Include), NullIfEmpty(dep.Exclude)),
                StringComparer.OrdinalIgnoreCase);

            EvaluateExpectedDependencies(
                checks,
                family,
                managedPackagePath,
                dependencyContracts,
                expectedVersion,
                manifestConfig);

            if (baselineContracts is null)
            {
                baselineContracts = dependencyContracts;
                continue;
            }

            if (!interGroupInconsistent && !HaveSameDependencies(baselineContracts, dependencyContracts))
            {
                interGroupInconsistent = true;
                AddCheck(checks, isValid: false, code: "G22", name: "TFM agreement",
                    message: $"G22: managed package '{managedPackagePath.GetFilename().FullPath}' emitted inconsistent dependency groups across target frameworks.");
            }
        }
    }

    [SuppressMessage("Design", "MA0051:Method is too long",
        Justification = "G21 within-family + cross-family dependency contract walking stays co-located so the full minimum-range assertion is readable end-to-end.")]
    private static void EvaluateExpectedDependencies(
        List<ValidationCheck> checks,
        PackageFamilyConfig family,
        FilePath managedPackagePath,
        Dictionary<string, DependencyContract> dependencyContracts,
        string expectedVersion,
        ManifestConfig manifestConfig)
    {
        // G21: within-family Native remains bare minimum range (`x.y.z`).
        // Cross-family dependencies must keep the same lower bound semantics (`>= x.y.z`),
        // while G56 separately enforces the explicit upper bound `< (UpstreamMajor + 1).0.0`.
        var expectedNativePackageId = FamilyIdentifierConventions.NativePackageId(family.Name);

        if (!dependencyContracts.TryGetValue(expectedNativePackageId, out var nativeContract))
        {
            AddFamilyDependencyCheck(checks, isValid: false,
                $"G21: managed package '{managedPackagePath.GetFilename().FullPath}' must depend on within-family '{expectedNativePackageId}' as bare minimum range '{expectedVersion}' (no brackets). Actual: '<missing>'.");
        }
        else
        {
            var bracketed = nativeContract.Version.Contains('[', StringComparison.Ordinal) || nativeContract.Version.Contains(']', StringComparison.Ordinal);
            var versionMatch = string.Equals(nativeContract.Version, expectedVersion, StringComparison.Ordinal);
            var nativeValid = !bracketed && versionMatch;

            AddFamilyDependencyCheck(checks, isValid: nativeValid,
                $"G21: managed package '{managedPackagePath.GetFilename().FullPath}' must depend on within-family '{expectedNativePackageId}' as bare minimum range '{expectedVersion}' (no brackets). Actual: '{nativeContract.Version}'.");

            if (!string.IsNullOrWhiteSpace(nativeContract.Exclude) &&
                nativeContract.Exclude.Contains("Build", StringComparison.OrdinalIgnoreCase))
            {
                AddFamilyDependencyCheck(checks, isValid: false,
                    $"G21: managed package '{managedPackagePath.GetFilename().FullPath}' must not exclude build assets on within-family native dependency '{expectedNativePackageId}'. Actual exclude='{nativeContract.Exclude}'. This would suppress native buildTransitive targets for .NET Framework consumers.");
            }

            if (!string.IsNullOrWhiteSpace(nativeContract.Include) &&
                !string.Equals(nativeContract.Include, "All", StringComparison.OrdinalIgnoreCase))
            {
                AddFamilyDependencyCheck(checks, isValid: false,
                    $"G21: managed package '{managedPackagePath.GetFilename().FullPath}' must keep within-family native dependency '{expectedNativePackageId}' build assets visible. Actual include='{nativeContract.Include}', expected 'All' or omitted.");
            }
        }

        foreach (var dependencyFamily in family.DependsOn)
        {
            var expectedManagedPackageId = FamilyIdentifierConventions.ManagedPackageId(dependencyFamily);

            if (!dependencyContracts.TryGetValue(expectedManagedPackageId, out var dependencyContract))
            {
                AddFamilyDependencyCheck(checks, isValid: false,
                    $"G21: managed package '{managedPackagePath.GetFilename().FullPath}' must declare cross-family '{expectedManagedPackageId}' with lower bound '>={expectedVersion}'. Actual: '<missing>'.");

                AddIfPresent(checks, SatelliteUpperBoundValidator.Validate(
                    family,
                    managedPackagePath,
                    dependencyFamily,
                    expectedManagedPackageId,
                    dependencyVersionExpression: null,
                    manifestConfig));
                continue;
            }

            var crossLowerBoundValid = MatchesExpectedLowerBound(dependencyContract.Version, expectedVersion);

            AddFamilyDependencyCheck(checks, isValid: crossLowerBoundValid,
                $"G21: managed package '{managedPackagePath.GetFilename().FullPath}' must declare cross-family '{expectedManagedPackageId}' with lower bound '>={expectedVersion}'. Actual expression: '{dependencyContract.Version}'.");

            AddIfPresent(checks, SatelliteUpperBoundValidator.Validate(
                family,
                managedPackagePath,
                dependencyFamily,
                expectedManagedPackageId,
                dependencyContract.Version,
                manifestConfig));
        }

        var expectedDependencyCount = 1 + family.DependsOn.Count;
        if (dependencyContracts.Count != expectedDependencyCount)
        {
            AddCheck(checks, isValid: false, code: "G22", name: "TFM agreement",
                message:
                $"G22: managed package '{managedPackagePath.GetFilename().FullPath}' emitted {dependencyContracts.Count} dependencies, expected {expectedDependencyCount}.");
        }
    }

    private static void AddFamilyDependencyCheck(
        List<ValidationCheck> checks,
        bool isValid,
        string message)
        => AddCheck(checks, isValid, code: "G21", name: "Within-family minimum range", message);

    private static bool MatchesExpectedLowerBound(string dependencyExpression, string expectedVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dependencyExpression);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedVersion);

        if (string.Equals(dependencyExpression, expectedVersion, StringComparison.Ordinal))
        {
            return true;
        }

        if (!VersionRange.TryParse(dependencyExpression, out var range))
        {
            return false;
        }

        if (range.MinVersion is null || !range.IsMinInclusive)
        {
            return false;
        }

        if (!NuGetVersion.TryParse(expectedVersion, out var expectedLowerBound))
        {
            return false;
        }

        return range.MinVersion == expectedLowerBound;
    }

    private static void EvaluateWithinFamilyVersionCoherence(
        List<ValidationCheck> checks,
        XElement managedMetadata,
        XElement nativeMetadata,
        FilePath managedPackagePath,
        FilePath nativePackagePath)
    {
        var managedVersion = TryGetChildValue(managedMetadata, "version", out var missingManaged);
        var nativeVersion = TryGetChildValue(nativeMetadata, "version", out var missingNative);

        if (missingManaged || missingNative)
        {
            AddCheck(checks, isValid: false, code: "G23", name: "Managed/native version match",
                message:
                $"G23: within-family version coherence cannot be verified because one of the <version> elements is missing. managed='{managedPackagePath.GetFilename().FullPath}' native='{nativePackagePath.GetFilename().FullPath}'.");
            return;
        }

        var match = string.Equals(managedVersion, nativeVersion, StringComparison.Ordinal);
        AddCheck(checks, isValid: match, code: "G23", name: "Managed/native version match",
            message: $"G23: managed package '{managedPackagePath.GetFilename().FullPath}' version '{managedVersion}' does not match native package '{nativePackagePath.GetFilename().FullPath}' version '{nativeVersion}'. Within-family version mismatch detected post-pack.");
    }

    private async Task EvaluateManagedSymbolsAsync(
        List<ValidationCheck> checks,
        FilePath symbolsPackagePath)
    {
        var file = _fileSystem.GetFile(symbolsPackagePath);
        if (!file.Exists)
        {
            AddCheck(checks, isValid: false, code: "G25", name: "Symbol package presence",
                message: $"G25: managed symbol package '{symbolsPackagePath.GetFilename().FullPath}' was not produced.");
            return;
        }

        bool hasNuspec;
        bool hasPdb;
        try
        {
            await using var stream = file.OpenRead();
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);

            hasNuspec = archive.Entries.Any(entry => entry.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase));
            hasPdb = archive.Entries.Any(entry => entry.FullName.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
        {
            AddCheck(checks, isValid: false, code: "G25", name: "Symbol package presence",
                message: $"G25: managed symbol package '{symbolsPackagePath.GetFilename().FullPath}' could not be read: {ex.Message}");
            return;
        }

        var valid = hasNuspec && hasPdb;
        AddCheck(checks, isValid: valid, code: "G25", name: "Symbol package presence",
            message: $"G25: managed symbol package '{symbolsPackagePath.GetFilename().FullPath}' is invalid. Required entries: .nuspec and at least one .pdb.");
    }

    /// <summary>
    /// G47 + G48 — native package layout checks. Opens the native .nupkg once and
    /// inspects entries for the buildTransitive contract (G47) and the per-RID payload
    /// shape (G48). Both concerns live on the native package only.
    /// </summary>
    private async Task EvaluateNativePackageLayoutAsync(
        List<ValidationCheck> checks,
        PackageFamilyConfig family,
        FilePath nativePackagePath)
    {
        var file = _fileSystem.GetFile(nativePackagePath);
        if (!file.Exists)
        {
            // Prior NuspecLoad check already recorded this; do not double-report.
            return;
        }

        HashSet<string> entries;
        try
        {
            await using var stream = file.OpenRead();
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
            entries = archive.Entries
                .Select(entry => entry.FullName.Replace('\\', '/'))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
        {
            // NuspecLoad will have recorded the read failure already.
            return;
        }

        EvaluateBuildTransitiveContract(checks, family, nativePackagePath, entries);
        EvaluateNativePayloadShapePerRid(checks, family, nativePackagePath, entries);
        EvaluateLicensePayloadPresence(checks, nativePackagePath, entries);
    }

    /// <summary>
    /// G51 — the native package must ship at least one entry under <c>licenses/</c>.
    /// Last line of defence against the H1 failure mode: a Harvest run invalidates the
    /// consolidated license tree, the operator skips ConsolidateHarvest, the pack gate
    /// somehow passes anyway (future regression or CI-side bypass), and Pack produces a
    /// nupkg with native assets but zero third-party attribution. The upstream layers
    /// (Harvest invalidation + HarvestReadinessValidator receipt gate) should catch this first;
    /// G51 catches it if they don't.
    /// </summary>
    private static void EvaluateLicensePayloadPresence(
        List<ValidationCheck> checks,
        FilePath nativePackagePath,
        HashSet<string> entries)
    {
        var hasLicenseEntry = entries.Any(entry => entry.StartsWith("licenses/", StringComparison.OrdinalIgnoreCase));

        AddCheck(checks, isValid: hasLicenseEntry, code: "G51", name: "License payload",
            message: $"G51: native package '{nativePackagePath.GetFilename().FullPath}' contains no entries under 'licenses/'. Third-party license attribution missing — consumer-side compliance surface is broken. Ensure Harvest + ConsolidateHarvest populated licenses/_consolidated/ before Package.");
    }

    private static void EvaluateBuildTransitiveContract(
        List<ValidationCheck> checks,
        PackageFamilyConfig family,
        FilePath nativePackagePath,
        HashSet<string> entries)
    {
        var nativePackageId = FamilyIdentifierConventions.NativePackageId(family.Name);
        var wrapperPath = $"buildTransitive/{nativePackageId}.targets";
        var sharedPath = "buildTransitive/Janset.SDL2.Native.Common.targets";

        var hasWrapper = entries.Contains(wrapperPath);
        var hasShared = entries.Contains(sharedPath);

        var missing = new List<string>();
        if (!hasWrapper)
        {
            missing.Add(wrapperPath);
        }
        if (!hasShared)
        {
            missing.Add(sharedPath);
        }

        var valid = missing.Count == 0;
        AddCheck(checks, isValid: valid, code: "G47", name: "BuildTransitive contract",
            message: $"G47: native package '{nativePackagePath.GetFilename().FullPath}' is missing required buildTransitive entry/entries: {string.Join(", ", missing)}. Consumers on Linux/macOS will not extract native.tar.gz; .NETFramework AnyCPU consumers will not receive the per-RID DLL copy.");
    }

    private static void EvaluateNativePayloadShapePerRid(
        List<ValidationCheck> checks,
        PackageFamilyConfig family,
        FilePath nativePackagePath,
        HashSet<string> entries)
    {
        // Discover the RID subtrees actually shipped under runtimes/<rid>/native/.
        // Using ordinal comparison is correct here — NuGet paths are always lowercase-invariant
        // and forward-slashed after the Replace we applied during hydration.
        var ridRoots = entries
            .Where(entry => entry.StartsWith("runtimes/", StringComparison.Ordinal))
            .Select(entry => entry[("runtimes/".Length)..])
            .Where(entry => entry.Contains("/native/", StringComparison.Ordinal))
            .Select(entry => entry[..entry.IndexOf("/native/", StringComparison.Ordinal)])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (ridRoots.Count == 0)
        {
            AddCheck(checks, isValid: false, code: "G48", name: "Native payload shape",
                message: $"G48: native package '{nativePackagePath.GetFilename().FullPath}' ships no runtimes/<rid>/native/ subtree. Consumer restore will have nothing to resolve.");
            return;
        }

        var nativePackageId = FamilyIdentifierConventions.NativePackageId(family.Name);
        var expectedTarballName = $"{nativePackageId}.tar.gz";

        foreach (var rid in ridRoots.OrderBy(rid => rid, StringComparer.OrdinalIgnoreCase))
        {
            var ridPrefix = $"runtimes/{rid}/native/";
            var payloadEntries = entries
                .Where(entry => entry.StartsWith(ridPrefix, StringComparison.OrdinalIgnoreCase))
                .Select(entry => entry[ridPrefix.Length..])
                .Where(entry => !string.IsNullOrWhiteSpace(entry) && !entry.Contains('/', StringComparison.Ordinal))
                .ToList();

            var dlls = payloadEntries
                .Where(entry => entry.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                .ToList();
            var tarballs = payloadEntries
                .Where(entry => entry.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var isWindowsRid = rid.StartsWith("win-", StringComparison.OrdinalIgnoreCase);

            if (isWindowsRid)
            {
                // Windows RIDs: expect at least one DLL, no tarballs.
                var windowsValid = dlls.Count > 0 && tarballs.Count == 0;
                AddCheck(checks, isValid: windowsValid, code: "G48", name: "Native payload shape",
                    message: $"G48: native package '{nativePackagePath.GetFilename().FullPath}' runtimes/{rid}/native/ layout is invalid. Expected one or more *.dll files, found dlls={dlls.Count} tarballs={tarballs.Count}.");
                continue;
            }

            // Unix RIDs (linux-*, osx-*): expect exactly one {PackageId}.tar.gz archive.
            // Filename must match $(PackageId).tar.gz so siblings do not collide when the
            // SDK flattens runtimes/<rid>/native/ into $(OutDir) on the consumer side.
            var unixValid = tarballs.Count == 1 &&
                            string.Equals(tarballs[0], expectedTarballName, StringComparison.Ordinal);
            var actualLabel = tarballs.Count == 0 ? "<none>" : string.Join(", ", tarballs);
            AddCheck(checks, isValid: unixValid, code: "G48", name: "Native payload shape",
                message: $"G48: native package '{nativePackagePath.GetFilename().FullPath}' runtimes/{rid}/native/ must contain exactly one '{expectedTarballName}'. Actual: {actualLabel}. Rename drift would cause consumer-side collision with sibling satellites.");
        }
    }

    private static string? TryGetChildValue(XElement parent, string localName, out bool missing)
    {
        var element = parent.Elements().SingleOrDefault(candidate => string.Equals(candidate.Name.LocalName, localName, StringComparison.Ordinal));
        if (element is null)
        {
            missing = true;
            return null;
        }

        missing = false;
        return element.Value.Trim();
    }

    private static bool HaveSameDependencies(
        Dictionary<string, DependencyContract> left,
        Dictionary<string, DependencyContract> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        foreach (var pair in left)
        {
            if (!right.TryGetValue(pair.Key, out var rightValue) ||
                !string.Equals(pair.Value.Version, rightValue.Version, StringComparison.Ordinal) ||
                !string.Equals(pair.Value.Include, rightValue.Include, StringComparison.Ordinal) ||
                !string.Equals(pair.Value.Exclude, rightValue.Exclude, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static string FormatFramework(NuGetFramework framework)
    {
        return framework.GetShortFolderName();
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private sealed record DependencyContract(string Version, string? Include, string? Exclude);

    private sealed record PackageNuspec(XDocument Document, string RawXml);
}
