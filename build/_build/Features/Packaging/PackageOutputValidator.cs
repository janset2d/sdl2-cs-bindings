using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using Build.Features.Preflight;
using Build.Shared.Manifest;
using Build.Shared.Packaging;
using Cake.Core.IO;
using NuGet.Frameworks;
using NuGet.Versioning;

namespace Build.Features.Packaging;

/// <summary>
/// Post-pack nuspec assertions for a packed family (one managed + one native .nupkg).
/// </summary>
/// <remarks>
/// Post-S1 scope (2026-04-17): guardrails G20 (within-family exact-pin `[x.y.z]` assertion)
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
/// Every guardrail is evaluated and added to the returned <see cref="PackageValidation"/>.
/// Operators see the full failure set instead of the first-throw-wins subset produced by the
/// pre-Result-pattern surface.
/// </remarks>
public sealed class PackageOutputValidator(
    IFileSystem fileSystem,
    NativePackageMetadataValidator nativePackageMetadataValidator,
    ReadmeMappingTableValidator readmeMappingTableValidator) : IPackageOutputValidator
{
    private readonly IFileSystem _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    private readonly NativePackageMetadataValidator _nativePackageMetadataValidator = nativePackageMetadataValidator ?? throw new ArgumentNullException(nameof(nativePackageMetadataValidator));
    private readonly ReadmeMappingTableValidator _readmeMappingTableValidator = readmeMappingTableValidator ?? throw new ArgumentNullException(nameof(readmeMappingTableValidator));

    public async Task<PackageValidationResult> ValidateAsync(
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

        var checks = new List<PackageValidationCheck>();

        AddProjectMetadataCompletenessChecks(checks, family, managedProjectMetadata);

        var managedNuspec = await TryLoadNuspecAsync(checks, family, artifacts.ManagedPackage, "managed package");
        var nativeNuspec = await TryLoadNuspecAsync(checks, family, artifacts.NativePackage, "native package");

        var managedMetadata = TryGetMetadataRoot(checks, family, artifacts.ManagedPackage, managedNuspec);
        var nativeMetadata = TryGetMetadataRoot(checks, family, artifacts.NativePackage, nativeNuspec);

        if (managedMetadata is not null)
        {
            EvaluateCanonicalMetadata(
                checks,
                family,
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
                family,
                artifacts.NativePackage,
                nativeMetadata,
                FamilyIdentifierConventions.NativePackageId(family.Name),
                expectedVersion,
                expectedCommitSha,
                managedProjectMetadata);
        }

        if (managedMetadata is not null && nativeMetadata is not null)
        {
            EvaluateWithinFamilyVersionCoherence(checks, family, managedMetadata, nativeMetadata, artifacts.ManagedPackage, artifacts.NativePackage);
        }

        await EvaluateManagedSymbolsAsync(checks, family, artifacts.ManagedSymbolsPackage);
        await EvaluateNativePackageLayoutAsync(checks, family, artifacts.NativePackage);
        checks.Add(await _nativePackageMetadataValidator.ValidateAsync(family, artifacts.NativePackage, expectedVersion, expectedCommitSha, manifestConfig));
        checks.Add(_readmeMappingTableValidator.Validate(family, readmePath, manifestConfig));

        var validation = new PackageValidation(checks);

        return validation.HasErrors
            ? PackageValidationResult.Fail(validation)
            : PackageValidationResult.Pass(validation);
    }

    private static void AddProjectMetadataCompletenessChecks(
        List<PackageValidationCheck> checks,
        PackageFamilyConfig family,
        ProjectMetadata metadata)
    {
        AddCompletenessCheck(
            checks,
            family,
            metadata.TargetFrameworks.Count != 0,
            expected: ">=1 TFM",
            actual: metadata.TargetFrameworks.Count.ToString(CultureInfo.InvariantCulture),
            message:
            $"ProjectMetadata for family '{family.Name}' has no target frameworks. Check that the managed csproj declares <TargetFrameworks> (directly or via Directory.Build.props).");

        AddCompletenessCheck(
            checks,
            family,
            !string.IsNullOrWhiteSpace(metadata.Authors),
            expected: "<non-empty>",
            actual: metadata.Authors,
            message: $"ProjectMetadata for family '{family.Name}' is missing Authors. Expected value from Directory.Build.props.");

        AddCompletenessCheck(
            checks,
            family,
            !string.IsNullOrWhiteSpace(metadata.PackageLicenseFile),
            expected: "<non-empty>",
            actual: metadata.PackageLicenseFile,
            message: $"ProjectMetadata for family '{family.Name}' is missing PackageLicenseFile. Expected value from Directory.Build.props.");

        AddCompletenessCheck(
            checks,
            family,
            !string.IsNullOrWhiteSpace(metadata.PackageIcon),
            expected: "<non-empty>",
            actual: metadata.PackageIcon,
            message: $"ProjectMetadata for family '{family.Name}' is missing PackageIcon. Expected value from Directory.Build.props.");
    }

    private static void AddCompletenessCheck(
        List<PackageValidationCheck> checks,
        PackageFamilyConfig family,
        bool isValid,
        string expected,
        string actual,
        string message)
    {
        checks.Add(new PackageValidationCheck(
            FamilyIdentifier: family.Name,
            PackagePath: null,
            Kind: PackageValidationCheckKind.ProjectMetadataComplete,
            IsValid: isValid,
            ExpectedValue: expected,
            ActualValue: actual,
            ErrorMessage: isValid ? null : message));
    }

    private async Task<PackageNuspec?> TryLoadNuspecAsync(
        List<PackageValidationCheck> checks,
        PackageFamilyConfig family,
        FilePath packagePath,
        string description)
    {
        var file = _fileSystem.GetFile(packagePath);
        if (!file.Exists)
        {
            checks.Add(new PackageValidationCheck(
                FamilyIdentifier: family.Name,
                PackagePath: packagePath,
                Kind: PackageValidationCheckKind.NuspecLoad,
                IsValid: false,
                ExpectedValue: "<.nupkg exists>",
                ActualValue: "<missing>",
                ErrorMessage: $"Post-pack assertion failed: expected {description} '{packagePath.GetFilename().FullPath}' was not produced."));
            return null;
        }

        try
        {
            await using var stream = file.OpenRead();
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);

            var nuspecEntry = archive.Entries.SingleOrDefault(entry => entry.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase));
            if (nuspecEntry is null)
            {
                checks.Add(new PackageValidationCheck(
                    FamilyIdentifier: family.Name,
                    PackagePath: packagePath,
                    Kind: PackageValidationCheckKind.NuspecLoad,
                    IsValid: false,
                    ExpectedValue: "<.nuspec entry>",
                    ActualValue: "<missing>",
                    ErrorMessage: $"Post-pack assertion failed: package '{packagePath.GetFilename().FullPath}' does not contain a .nuspec entry."));
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
            checks.Add(new PackageValidationCheck(
                FamilyIdentifier: family.Name,
                PackagePath: packagePath,
                Kind: PackageValidationCheckKind.NuspecLoad,
                IsValid: false,
                ExpectedValue: "<parseable nuspec>",
                ActualValue: ex.Message,
                ErrorMessage: $"Post-pack assertion failed: could not read nuspec in '{packagePath.GetFilename().FullPath}': {ex.Message}"));
            return null;
        }
    }

    private static XElement? TryGetMetadataRoot(
        List<PackageValidationCheck> checks,
        PackageFamilyConfig family,
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
            checks.Add(new PackageValidationCheck(
                FamilyIdentifier: family.Name,
                PackagePath: packagePath,
                Kind: PackageValidationCheckKind.NuspecLoad,
                IsValid: false,
                ExpectedValue: "<metadata element>",
                ActualValue: "<missing>",
                ErrorMessage: $"Post-pack assertion failed: package '{packagePath.GetFilename().FullPath}' is missing required element 'metadata'."));
        }

        return metadata;
    }

    [SuppressMessage("Design", "MA0051:Method is too long",
        Justification = "Six sequential canonical checks kept co-located for readability; splitting hurts traceability to guardrails G26/G27.")]
    private static void EvaluateCanonicalMetadata(
        List<PackageValidationCheck> checks,
        PackageFamilyConfig family,
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
            family,
            packagePath,
            isValid: !missingId && string.Equals(packageId, expectedPackageId, StringComparison.Ordinal),
            expected: expectedPackageId,
            actual: missingId ? "<missing>" : packageId,
            message: $"G27: package '{packagePath.GetFilename().FullPath}' emitted id '{packageId ?? "<missing>"}', expected '{expectedPackageId}'.");

        var version = TryGetChildValue(metadata, "version", out var missingVersion);
        AddCanonicalCheck(
            checks,
            family,
            packagePath,
            isValid: !missingVersion && string.Equals(version, expectedVersion, StringComparison.Ordinal),
            expected: expectedVersion,
            actual: missingVersion ? "<missing>" : version,
            message: $"G23/G27: package '{packagePath.GetFilename().FullPath}' emitted version '{version ?? "<missing>"}', expected '{expectedVersion}'.");

        var authors = TryGetChildValue(metadata, "authors", out var missingAuthors);
        AddCanonicalCheck(
            checks,
            family,
            packagePath,
            isValid: !missingAuthors && string.Equals(authors, projectMetadata.Authors, StringComparison.Ordinal),
            expected: projectMetadata.Authors,
            actual: missingAuthors ? "<missing>" : authors,
            message:
            $"G27: package '{packagePath.GetFilename().FullPath}' emitted authors '{authors ?? "<missing>"}', expected '{projectMetadata.Authors}' (resolved from Directory.Build.props).");

        var license = metadata.Elements().SingleOrDefault(element => string.Equals(element.Name.LocalName, "license", StringComparison.Ordinal));
        if (license is null)
        {
            AddCanonicalCheck(
                checks,
                family,
                packagePath,
                isValid: false,
                expected: $"file:{projectMetadata.PackageLicenseFile}",
                actual: "<missing>",
                message: $"G27: package '{packagePath.GetFilename().FullPath}' is missing required element 'license'.");
        }
        else
        {
            var licenseType = license.Attributes().SingleOrDefault(attr => string.Equals(attr.Name.LocalName, "type", StringComparison.Ordinal))?.Value?.Trim();
            var licenseValue = license.Value.Trim();

            var licenseValid = string.Equals(licenseType, "file", StringComparison.OrdinalIgnoreCase) &&
                               string.Equals(licenseValue, projectMetadata.PackageLicenseFile, StringComparison.Ordinal);

            AddCanonicalCheck(
                checks,
                family,
                packagePath,
                isValid: licenseValid,
                expected: $"file:{projectMetadata.PackageLicenseFile}",
                actual: $"{licenseType ?? "<missing>"}:{licenseValue}",
                message:
                $"G27: package '{packagePath.GetFilename().FullPath}' emitted license '{licenseValue}' type '{licenseType ?? "<missing>"}', expected file '{projectMetadata.PackageLicenseFile}'.");
        }

        var icon = TryGetChildValue(metadata, "icon", out var missingIcon);
        AddCanonicalCheck(
            checks,
            family,
            packagePath,
            isValid: !missingIcon && string.Equals(icon, projectMetadata.PackageIcon, StringComparison.Ordinal),
            expected: projectMetadata.PackageIcon,
            actual: missingIcon ? "<missing>" : icon,
            message: $"G27: package '{packagePath.GetFilename().FullPath}' emitted icon '{icon ?? "<missing>"}', expected '{projectMetadata.PackageIcon}'.");

        var repository = metadata.Elements().SingleOrDefault(element => string.Equals(element.Name.LocalName, "repository", StringComparison.Ordinal));
        if (repository is null)
        {
            AddCanonicalCheck(
                checks,
                family,
                packagePath,
                isValid: false,
                expected: expectedCommitSha,
                actual: "<missing>",
                message: $"G26: package '{packagePath.GetFilename().FullPath}' is missing required element 'repository'.");
        }
        else
        {
            var actualCommit = repository.Attributes().SingleOrDefault(attr => string.Equals(attr.Name.LocalName, "commit", StringComparison.Ordinal))?.Value?.Trim() ??
                               string.Empty;
            var commitValid = string.Equals(actualCommit, expectedCommitSha, StringComparison.OrdinalIgnoreCase);
            AddCanonicalCheck(
                checks,
                family,
                packagePath,
                isValid: commitValid,
                expected: expectedCommitSha,
                actual: string.IsNullOrWhiteSpace(actualCommit) ? "<missing>" : actualCommit,
                message: $"G26: package '{packagePath.GetFilename().FullPath}' emitted repository commit '{actualCommit}', expected '{expectedCommitSha}'.");
        }
    }

    private static void AddCanonicalCheck(
        List<PackageValidationCheck> checks,
        PackageFamilyConfig family,
        FilePath packagePath,
        bool isValid,
        string expected,
        string? actual,
        string message)
    {
        checks.Add(new PackageValidationCheck(
            FamilyIdentifier: family.Name,
            PackagePath: packagePath,
            Kind: PackageValidationCheckKind.CanonicalMetadataMatches,
            IsValid: isValid,
            ExpectedValue: expected,
            ActualValue: actual,
            ErrorMessage: isValid ? null : message));
    }

    [SuppressMessage("Design", "MA0051:Method is too long",
        Justification =
            "Dependency-group walking interleaves framework parity, inter-group consistency, and per-group expected-dependency checks (G21/G22) — splitting them obscures the per-framework control flow.")]
    private static void EvaluateDependencyGroups(
        List<PackageValidationCheck> checks,
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
            checks.Add(new PackageValidationCheck(
                FamilyIdentifier: family.Name,
                PackagePath: managedPackagePath,
                Kind: PackageValidationCheckKind.DependencyGroupsConsistentAcrossFrameworks,
                IsValid: false,
                ExpectedValue: "<dependencies>",
                ActualValue: "<missing>",
                ErrorMessage: $"G21-G22: managed package '{managedPackagePath.GetFilename().FullPath}' is missing a <dependencies> section."));
            return;
        }

        var groups = dependencies.Elements()
            .Where(element => string.Equals(element.Name.LocalName, "group", StringComparison.Ordinal))
            .ToList();

        if (groups.Count == 0)
        {
            checks.Add(new PackageValidationCheck(
                FamilyIdentifier: family.Name,
                PackagePath: managedPackagePath,
                Kind: PackageValidationCheckKind.DependencyGroupsConsistentAcrossFrameworks,
                IsValid: false,
                ExpectedValue: ">=1 group",
                ActualValue: "0",
                ErrorMessage: $"G21-G22: managed package '{managedPackagePath.GetFilename().FullPath}' emitted no dependency groups."));
            return;
        }

        var expectedFrameworks = projectMetadata.TargetFrameworks
            .Select(NuGetFramework.Parse)
            .ToHashSet();

        var actualFrameworksByKey = new List<(XElement Group, NuGetFramework Framework)>();
        foreach (var group in groups)
        {
            var tfmAttr = group.Attributes().SingleOrDefault(attr => string.Equals(attr.Name.LocalName, "targetFramework", StringComparison.Ordinal))?.Value?.Trim();
            if (string.IsNullOrWhiteSpace(tfmAttr))
            {
                checks.Add(new PackageValidationCheck(
                    FamilyIdentifier: family.Name,
                    PackagePath: managedPackagePath,
                    Kind: PackageValidationCheckKind.DependencyGroupsConsistentAcrossFrameworks,
                    IsValid: false,
                    ExpectedValue: "<targetFramework>",
                    ActualValue: "<missing>",
                    ErrorMessage:
                    $"G22: managed package '{managedPackagePath.GetFilename().FullPath}' has a dependency group missing required attribute 'group/@targetFramework'."));
                return;
            }

            actualFrameworksByKey.Add((group, NuGetFramework.Parse(tfmAttr)));
        }

        var actualFrameworks = actualFrameworksByKey.Select(item => item.Framework).ToHashSet();

        var frameworksMatch = actualFrameworks.SetEquals(expectedFrameworks);
        if (!frameworksMatch)
        {
            var expectedLabel = string.Join(", ", expectedFrameworks.Select(FormatFramework).OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
            var actualLabel = string.Join(", ", actualFrameworks.Select(FormatFramework).OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
            checks.Add(new PackageValidationCheck(
                FamilyIdentifier: family.Name,
                PackagePath: managedPackagePath,
                Kind: PackageValidationCheckKind.DependencyGroupsConsistentAcrossFrameworks,
                IsValid: false,
                ExpectedValue: expectedLabel,
                ActualValue: actualLabel,
                ErrorMessage:
                $"G22: managed package '{managedPackagePath.GetFilename().FullPath}' emitted framework groups '{actualLabel}', expected '{expectedLabel}' (resolved from csproj)."));
        }

        Dictionary<string, DependencyContract>? baselineContracts = null;
        var interGroupInconsistent = false;

        foreach (var (group, _) in actualFrameworksByKey)
        {
            var dependencyContracts = group.Elements()
                .Where(element => string.Equals(element.Name.LocalName, "dependency", StringComparison.Ordinal))
                .Select(element => new
                {
                    Id = element.Attributes().SingleOrDefault(attr => string.Equals(attr.Name.LocalName, "id", StringComparison.Ordinal))?.Value?.Trim(),
                    Version = element.Attributes().SingleOrDefault(attr => string.Equals(attr.Name.LocalName, "version", StringComparison.Ordinal))?.Value?.Trim(),
                    Include = element.Attributes().SingleOrDefault(attr => string.Equals(attr.Name.LocalName, "include", StringComparison.Ordinal))?.Value?.Trim(),
                    Exclude = element.Attributes().SingleOrDefault(attr => string.Equals(attr.Name.LocalName, "exclude", StringComparison.Ordinal))?.Value?.Trim(),
                })
                .Where(dep => !string.IsNullOrWhiteSpace(dep.Id) && !string.IsNullOrWhiteSpace(dep.Version))
                .ToDictionary(
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
                checks.Add(new PackageValidationCheck(
                    FamilyIdentifier: family.Name,
                    PackagePath: managedPackagePath,
                    Kind: PackageValidationCheckKind.DependencyGroupsConsistentAcrossFrameworks,
                    IsValid: false,
                    ExpectedValue: "identical per-group dependency sets",
                    ActualValue: "divergent",
                    ErrorMessage: $"G22: managed package '{managedPackagePath.GetFilename().FullPath}' emitted inconsistent dependency groups across target frameworks."));
            }
        }
    }

    [SuppressMessage("Design", "MA0051:Method is too long",
        Justification = "G21 within-family + cross-family dependency contract walking stays co-located so the full minimum-range assertion is readable end-to-end.")]
    private static void EvaluateExpectedDependencies(
        List<PackageValidationCheck> checks,
        PackageFamilyConfig family,
        FilePath managedPackagePath,
        Dictionary<string, DependencyContract> dependencyContracts,
        string expectedVersion,
        ManifestConfig manifestConfig)
    {
        // G21 (post-ADR-001): within-family Native remains bare minimum range (`x.y.z`).
        // Cross-family dependencies must keep the same lower bound semantics (`>= x.y.z`),
        // while G56 separately enforces the explicit upper bound `< (UpstreamMajor + 1).0.0`.
        var expectedNativePackageId = FamilyIdentifierConventions.NativePackageId(family.Name);

        if (!dependencyContracts.TryGetValue(expectedNativePackageId, out var nativeContract))
        {
            checks.Add(BuildFamilyDependencyCheck(
                family,
                managedPackagePath,
                isValid: false,
                expected: expectedVersion,
                actual: "<missing>",
                message:
                $"G21: managed package '{managedPackagePath.GetFilename().FullPath}' must depend on within-family '{expectedNativePackageId}' as bare minimum range '{expectedVersion}' (no brackets). Actual: '<missing>'."));
        }
        else
        {
            var bracketed = nativeContract.Version.Contains('[', StringComparison.Ordinal) || nativeContract.Version.Contains(']', StringComparison.Ordinal);
            var versionMatch = string.Equals(nativeContract.Version, expectedVersion, StringComparison.Ordinal);
            var nativeValid = !bracketed && versionMatch;

            checks.Add(BuildFamilyDependencyCheck(
                family,
                managedPackagePath,
                isValid: nativeValid,
                expected: expectedVersion,
                actual: nativeContract.Version,
                message:
                $"G21: managed package '{managedPackagePath.GetFilename().FullPath}' must depend on within-family '{expectedNativePackageId}' as bare minimum range '{expectedVersion}' (no brackets). Actual: '{nativeContract.Version}'."));

            if (!string.IsNullOrWhiteSpace(nativeContract.Exclude) &&
                nativeContract.Exclude.Contains("Build", StringComparison.OrdinalIgnoreCase))
            {
                checks.Add(BuildFamilyDependencyCheck(
                    family,
                    managedPackagePath,
                    isValid: false,
                    expected: "exclude=<nothing or Analyzers-only>",
                    actual: $"exclude={nativeContract.Exclude}",
                    message:
                    $"G21: managed package '{managedPackagePath.GetFilename().FullPath}' must not exclude build assets on within-family native dependency '{expectedNativePackageId}'. Actual exclude='{nativeContract.Exclude}'. This would suppress native buildTransitive targets for .NET Framework consumers."));
            }

            if (!string.IsNullOrWhiteSpace(nativeContract.Include) &&
                !string.Equals(nativeContract.Include, "All", StringComparison.OrdinalIgnoreCase))
            {
                checks.Add(BuildFamilyDependencyCheck(
                    family,
                    managedPackagePath,
                    isValid: false,
                    expected: "include=All (or omitted)",
                    actual: $"include={nativeContract.Include}",
                    message:
                    $"G21: managed package '{managedPackagePath.GetFilename().FullPath}' must keep within-family native dependency '{expectedNativePackageId}' build assets visible. Actual include='{nativeContract.Include}', expected 'All' or omitted."));
            }
        }

        foreach (var dependencyFamily in family.DependsOn)
        {
            var expectedManagedPackageId = FamilyIdentifierConventions.ManagedPackageId(dependencyFamily);

            if (!dependencyContracts.TryGetValue(expectedManagedPackageId, out var dependencyContract))
            {
                checks.Add(BuildFamilyDependencyCheck(
                    family,
                    managedPackagePath,
                    isValid: false,
                    expected: expectedVersion,
                    actual: "<missing>",
                    message:
                    $"G21: managed package '{managedPackagePath.GetFilename().FullPath}' must declare cross-family '{expectedManagedPackageId}' with lower bound '>={expectedVersion}'. Actual: '<missing>'."));

                checks.Add(SatelliteUpperBoundValidator.Validate(
                    family,
                    managedPackagePath,
                    dependencyFamily,
                    expectedManagedPackageId,
                    dependencyVersionExpression: null,
                    manifestConfig));
                continue;
            }

            var crossLowerBoundValid = MatchesExpectedLowerBound(dependencyContract.Version, expectedVersion);

            checks.Add(BuildFamilyDependencyCheck(
                family,
                managedPackagePath,
                isValid: crossLowerBoundValid,
                expected: expectedVersion,
                actual: dependencyContract.Version,
                message:
                $"G21: managed package '{managedPackagePath.GetFilename().FullPath}' must declare cross-family '{expectedManagedPackageId}' with lower bound '>={expectedVersion}'. Actual expression: '{dependencyContract.Version}'."));

            checks.Add(SatelliteUpperBoundValidator.Validate(
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
            checks.Add(new PackageValidationCheck(
                FamilyIdentifier: family.Name,
                PackagePath: managedPackagePath,
                Kind: PackageValidationCheckKind.DependencyGroupsConsistentAcrossFrameworks,
                IsValid: false,
                ExpectedValue: expectedDependencyCount.ToString(CultureInfo.InvariantCulture),
                ActualValue: dependencyContracts.Count.ToString(CultureInfo.InvariantCulture),
                ErrorMessage:
                $"G22: managed package '{managedPackagePath.GetFilename().FullPath}' emitted {dependencyContracts.Count} dependencies, expected {expectedDependencyCount}."));
        }
    }

    private static PackageValidationCheck BuildFamilyDependencyCheck(
        PackageFamilyConfig family,
        FilePath managedPackagePath,
        bool isValid,
        string expected,
        string actual,
        string message)
    {
        return new PackageValidationCheck(
            FamilyIdentifier: family.Name,
            PackagePath: managedPackagePath,
            Kind: PackageValidationCheckKind.FamilyDependencyMinimumRange,
            IsValid: isValid,
            ExpectedValue: expected,
            ActualValue: actual,
            ErrorMessage: isValid ? null : message);
    }

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
        List<PackageValidationCheck> checks,
        PackageFamilyConfig family,
        XElement managedMetadata,
        XElement nativeMetadata,
        FilePath managedPackagePath,
        FilePath nativePackagePath)
    {
        var managedVersion = TryGetChildValue(managedMetadata, "version", out var missingManaged);
        var nativeVersion = TryGetChildValue(nativeMetadata, "version", out var missingNative);

        if (missingManaged || missingNative)
        {
            checks.Add(new PackageValidationCheck(
                FamilyIdentifier: family.Name,
                PackagePath: managedPackagePath,
                Kind: PackageValidationCheckKind.WithinFamilyVersionCoherence,
                IsValid: false,
                ExpectedValue: "managed == native <version>",
                ActualValue: $"managed='{managedVersion ?? "<missing>"}' native='{nativeVersion ?? "<missing>"}'",
                ErrorMessage:
                $"G23: within-family version coherence cannot be verified because one of the <version> elements is missing. managed='{managedPackagePath.GetFilename().FullPath}' native='{nativePackagePath.GetFilename().FullPath}'."));
            return;
        }

        var match = string.Equals(managedVersion, nativeVersion, StringComparison.Ordinal);
        checks.Add(new PackageValidationCheck(
            FamilyIdentifier: family.Name,
            PackagePath: managedPackagePath,
            Kind: PackageValidationCheckKind.WithinFamilyVersionCoherence,
            IsValid: match,
            ExpectedValue: managedVersion ?? string.Empty,
            ActualValue: nativeVersion ?? string.Empty,
            ErrorMessage: match
                ? null
                : $"G23: managed package '{managedPackagePath.GetFilename().FullPath}' version '{managedVersion}' does not match native package '{nativePackagePath.GetFilename().FullPath}' version '{nativeVersion}'. Within-family version mismatch detected post-pack."));
    }

    private async Task EvaluateManagedSymbolsAsync(
        List<PackageValidationCheck> checks,
        PackageFamilyConfig family,
        FilePath symbolsPackagePath)
    {
        var file = _fileSystem.GetFile(symbolsPackagePath);
        if (!file.Exists)
        {
            checks.Add(new PackageValidationCheck(
                FamilyIdentifier: family.Name,
                PackagePath: symbolsPackagePath,
                Kind: PackageValidationCheckKind.ManagedSymbolsPackageValid,
                IsValid: false,
                ExpectedValue: "<.snupkg exists>",
                ActualValue: "<missing>",
                ErrorMessage: $"G25: managed symbol package '{symbolsPackagePath.GetFilename().FullPath}' was not produced."));
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
            checks.Add(new PackageValidationCheck(
                FamilyIdentifier: family.Name,
                PackagePath: symbolsPackagePath,
                Kind: PackageValidationCheckKind.ManagedSymbolsPackageValid,
                IsValid: false,
                ExpectedValue: "<readable .snupkg>",
                ActualValue: ex.Message,
                ErrorMessage: $"G25: managed symbol package '{symbolsPackagePath.GetFilename().FullPath}' could not be read: {ex.Message}"));
            return;
        }

        var valid = hasNuspec && hasPdb;
        checks.Add(new PackageValidationCheck(
            FamilyIdentifier: family.Name,
            PackagePath: symbolsPackagePath,
            Kind: PackageValidationCheckKind.ManagedSymbolsPackageValid,
            IsValid: valid,
            ExpectedValue: ".nuspec + >=1 .pdb",
            ActualValue: $"nuspec={hasNuspec} pdb={hasPdb}",
            ErrorMessage: valid
                ? null
                : $"G25: managed symbol package '{symbolsPackagePath.GetFilename().FullPath}' is invalid. Required entries: .nuspec and at least one .pdb."));
    }

    /// <summary>
    /// G28 + G29 — native package layout checks. Opens the native .nupkg once and
    /// inspects entries for the buildTransitive contract (G28) and the per-RID payload
    /// shape (G29). Both concerns live on the native package only.
    /// </summary>
    private async Task EvaluateNativePackageLayoutAsync(
        List<PackageValidationCheck> checks,
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
        EvaluateLicensePayloadPresence(checks, family, nativePackagePath, entries);
    }

    /// <summary>
    /// G51 — the native package must ship at least one entry under <c>licenses/</c>.
    /// Last line of defence against the H1 failure mode: a Harvest run invalidates the
    /// consolidated license tree, the operator skips ConsolidateHarvest, the pack gate
    /// somehow passes anyway (future regression or CI-side bypass), and Pack produces a
    /// nupkg with native assets but zero third-party attribution. The upstream layers
    /// (Harvest invalidation + PackagePipeline receipt gate) should catch this first;
    /// G51 catches it if they don't.
    /// </summary>
    private static void EvaluateLicensePayloadPresence(
        List<PackageValidationCheck> checks,
        PackageFamilyConfig family,
        FilePath nativePackagePath,
        HashSet<string> entries)
    {
        var hasLicenseEntry = entries.Any(entry => entry.StartsWith("licenses/", StringComparison.OrdinalIgnoreCase));

        checks.Add(new PackageValidationCheck(
            FamilyIdentifier: family.Name,
            PackagePath: nativePackagePath,
            Kind: PackageValidationCheckKind.LicensePayloadPresent,
            IsValid: hasLicenseEntry,
            ExpectedValue: "at least one entry under licenses/",
            ActualValue: hasLicenseEntry ? "present" : "absent",
            ErrorMessage: hasLicenseEntry
                ? null
                : $"G51: native package '{nativePackagePath.GetFilename().FullPath}' contains no entries under 'licenses/'. Third-party license attribution missing — consumer-side compliance surface is broken. Ensure Harvest + ConsolidateHarvest populated licenses/_consolidated/ before Package."));
    }

    private static void EvaluateBuildTransitiveContract(
        List<PackageValidationCheck> checks,
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
        checks.Add(new PackageValidationCheck(
            FamilyIdentifier: family.Name,
            PackagePath: nativePackagePath,
            Kind: PackageValidationCheckKind.BuildTransitiveContractPresent,
            IsValid: valid,
            ExpectedValue: $"{wrapperPath} + {sharedPath}",
            ActualValue: valid ? "present" : $"missing: {string.Join(", ", missing)}",
            ErrorMessage: valid
                ? null
                : $"G47: native package '{nativePackagePath.GetFilename().FullPath}' is missing required buildTransitive entry/entries: {string.Join(", ", missing)}. Consumers on Linux/macOS will not extract native.tar.gz; .NETFramework AnyCPU consumers will not receive the per-RID DLL copy."));
    }

    private static void EvaluateNativePayloadShapePerRid(
        List<PackageValidationCheck> checks,
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
            checks.Add(new PackageValidationCheck(
                FamilyIdentifier: family.Name,
                PackagePath: nativePackagePath,
                Kind: PackageValidationCheckKind.NativePayloadShapePerRid,
                IsValid: false,
                ExpectedValue: ">=1 runtimes/<rid>/native/ subtree",
                ActualValue: "none",
                ErrorMessage: $"G48: native package '{nativePackagePath.GetFilename().FullPath}' ships no runtimes/<rid>/native/ subtree. Consumer restore will have nothing to resolve."));
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
                checks.Add(new PackageValidationCheck(
                    FamilyIdentifier: family.Name,
                    PackagePath: nativePackagePath,
                    Kind: PackageValidationCheckKind.NativePayloadShapePerRid,
                    IsValid: windowsValid,
                    ExpectedValue: $">=1 *.dll, 0 *.tar.gz (rid={rid})",
                    ActualValue: $"dlls={dlls.Count}, tarballs={tarballs.Count} (rid={rid})",
                    ErrorMessage: windowsValid
                        ? null
                        : $"G48: native package '{nativePackagePath.GetFilename().FullPath}' runtimes/{rid}/native/ layout is invalid. Expected one or more *.dll files, found dlls={dlls.Count} tarballs={tarballs.Count}."));
                continue;
            }

            // Unix RIDs (linux-*, osx-*): expect exactly one {PackageId}.tar.gz archive.
            // Filename must match $(PackageId).tar.gz so siblings do not collide when the
            // SDK flattens runtimes/<rid>/native/ into $(OutDir) on the consumer side.
            var unixValid = tarballs.Count == 1 &&
                            string.Equals(tarballs[0], expectedTarballName, StringComparison.Ordinal);
            var actualLabel = tarballs.Count == 0 ? "<no tarball>" : string.Join(", ", tarballs);
            var errorActualLabel = tarballs.Count == 0 ? "<none>" : string.Join(", ", tarballs);
            checks.Add(new PackageValidationCheck(
                FamilyIdentifier: family.Name,
                PackagePath: nativePackagePath,
                Kind: PackageValidationCheckKind.NativePayloadShapePerRid,
                IsValid: unixValid,
                ExpectedValue: $"exactly 1 '{expectedTarballName}' (rid={rid})",
                ActualValue: actualLabel,
                ErrorMessage: unixValid
                    ? null
                    : $"G48: native package '{nativePackagePath.GetFilename().FullPath}' runtimes/{rid}/native/ must contain exactly one '{expectedTarballName}'. Actual: {errorActualLabel}. Rename drift would cause consumer-side collision with sibling satellites."));
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
