using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Build.Context;
using Build.Context.Configs;
using Build.Context.Models;
using Build.Domain.Harvesting.Models;
using Build.Domain.Packaging;
using Build.Domain.Packaging.Models;
using Build.Domain.Packaging.Results;
using Build.Domain.Paths;
using Build.Domain.Preflight;
using Build.Infrastructure.DotNet;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Cake.Git;
using NuGet.Versioning;

namespace Build.Application.Packaging;

public sealed class PackageTaskRunner : IPackageTaskRunner
{
    private readonly ICakeContext _cakeContext;
    private readonly ICakeLog _log;
    private readonly IPathService _pathService;
    private readonly ManifestConfig _manifestConfig;
    private readonly DotNetBuildConfiguration _dotNetBuildConfiguration;
    private readonly IDotNetPackInvoker _dotNetPackInvoker;
    private readonly INativePackageMetadataGenerator _nativePackageMetadataGenerator;
    private readonly IReadmeMappingTableGenerator _readmeMappingTableGenerator;
    private readonly IProjectMetadataReader _projectMetadataReader;
    private readonly IPackageOutputValidator _packageOutputValidator;
    private readonly IG58CrossFamilyDepResolvabilityValidator _g58CrossFamilyDepResolvabilityValidator;

    /// <summary>
    /// HEAD SHA resolver. Default implementation delegates to Cake.Frosting.Git's
    /// <c>GitLogTip</c> alias (LibGit2Sharp-backed, in-process, no subprocess spawn).
    /// The delegate is exposed as a test-only optional ctor hook because Cake.Git bypasses
    /// <c>ICakeContext.FileSystem</c> and hits <c>System.IO</c> directly via LibGit2Sharp's
    /// native binary — which means unit tests using <c>FakeFileSystem</c> cannot be served
    /// by the default resolver. Tests inject a stub lambda; production uses the default.
    /// The end-to-end witness (smoke-witness.cs) exercises the default resolver against a
    /// real repo, so default behaviour stays covered without an explicit integration test.
    /// </summary>
    private readonly Func<string> _resolveHeadCommitSha;

    public PackageTaskRunner(
        ICakeContext cakeContext,
        ICakeLog log,
        IPathService pathService,
        ManifestConfig manifestConfig,
        DotNetBuildConfiguration dotNetBuildConfiguration,
        IDotNetPackInvoker dotNetPackInvoker,
        INativePackageMetadataGenerator nativePackageMetadataGenerator,
        IReadmeMappingTableGenerator readmeMappingTableGenerator,
        IProjectMetadataReader projectMetadataReader,
        IPackageOutputValidator packageOutputValidator,
        IG58CrossFamilyDepResolvabilityValidator g58CrossFamilyDepResolvabilityValidator,
        Func<ICakeContext, DirectoryPath, string>? resolveHeadCommitSha = null)
    {
        _cakeContext = cakeContext ?? throw new ArgumentNullException(nameof(cakeContext));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));
        _manifestConfig = manifestConfig ?? throw new ArgumentNullException(nameof(manifestConfig));
        _dotNetBuildConfiguration = dotNetBuildConfiguration ?? throw new ArgumentNullException(nameof(dotNetBuildConfiguration));
        _dotNetPackInvoker = dotNetPackInvoker ?? throw new ArgumentNullException(nameof(dotNetPackInvoker));
        _nativePackageMetadataGenerator = nativePackageMetadataGenerator ?? throw new ArgumentNullException(nameof(nativePackageMetadataGenerator));
        _readmeMappingTableGenerator = readmeMappingTableGenerator ?? throw new ArgumentNullException(nameof(readmeMappingTableGenerator));
        _projectMetadataReader = projectMetadataReader ?? throw new ArgumentNullException(nameof(projectMetadataReader));
        _packageOutputValidator = packageOutputValidator ?? throw new ArgumentNullException(nameof(packageOutputValidator));
        _g58CrossFamilyDepResolvabilityValidator = g58CrossFamilyDepResolvabilityValidator ?? throw new ArgumentNullException(nameof(g58CrossFamilyDepResolvabilityValidator));

        var resolver = resolveHeadCommitSha ?? DefaultResolveHeadCommitSha;
        _resolveHeadCommitSha = () => resolver(_cakeContext, _pathService.RepoRoot);
    }

    public async Task RunAsync(BuildContext context, PackRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var explicitVersions = request.Versions;
        if (explicitVersions.Count == 0)
        {
            throw new CakeException(
                "Package task requires at least one --explicit-version family=semver entry. " +
                "Stage targets consume the resolved version mapping directly, and the mapping " +
                "also defines package scope.");
        }

        // G58 runs again here as a pack-stage guard, even if the caller skipped PreFlight.
        var g58Validation = _g58CrossFamilyDepResolvabilityValidator.Validate(explicitVersions, _manifestConfig);
        if (g58Validation.HasErrors)
        {
            foreach (var errorCheck in g58Validation.Checks.Where(check => check.IsError))
            {
                _log.Error("G58 {0} → {1}: {2}", errorCheck.DependentFamily, errorCheck.DependencyFamily, errorCheck.ErrorMessage);
            }

            throw new CakeException(
                "Package task refused to proceed: G58 cross-family dependency resolvability detected " +
                $"{g58Validation.Checks.Count(check => check.IsError)} unresolved dependency/dependencies. " +
                "Either include the missing families in --explicit-version, or (post-C feed-probe wiring) pass --feed <URL> to enable target-feed resolution.");
        }

        var families = ResolveSelectedFamilies(explicitVersions);
        var expectedCommitSha = ResolveHeadCommitSha();

        // G57 generator: keep README mapping block aligned with manifest before pack validation.
        await _readmeMappingTableGenerator.UpdateAsync(cancellationToken);

        _cakeContext.EnsureDirectoryExists(_pathService.PackagesOutput);

        foreach (var family in families)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var familyVersion = explicitVersions[family.Name].ToNormalizedString();
            await PackFamilyAsync(family, familyVersion, expectedCommitSha, cancellationToken);
        }
    }

    private IReadOnlyList<PackageFamilyConfig> ResolveSelectedFamilies(IReadOnlyDictionary<string, NuGetVersion> explicitVersions)
    {
        var selectedFamilies = new List<PackageFamilyConfig>(explicitVersions.Count);

        foreach (var requestedFamily in explicitVersions.Keys)
        {
            var family = _manifestConfig.PackageFamilies.SingleOrDefault(candidate =>
                string.Equals(candidate.Name, requestedFamily, StringComparison.OrdinalIgnoreCase));

            if (family is null)
            {
                throw new CakeException(
                    $"Package task received unknown family '{requestedFamily}'. Add it to build/manifest.json package_families[] or fix the CLI value.");
            }

            if (string.IsNullOrWhiteSpace(family.ManagedProject) || string.IsNullOrWhiteSpace(family.NativeProject))
            {
                throw new CakeException(
                    $"Package task cannot pack family '{family.Name}' yet because manifest.json does not declare both managed_project and native_project. This usually means the family is still a placeholder.");
            }

            selectedFamilies.Add(family);
        }

        if (!FamilyTopologyHelpers.TryOrderByDependencies(selectedFamilies, out var orderedFamilies, out var errorMessage))
        {
            throw new CakeException(errorMessage);
        }

        return orderedFamilies;
    }

    private async Task PackFamilyAsync(PackageFamilyConfig family, string version, string expectedCommitSha, CancellationToken cancellationToken)
    {
        var managedProjectPath = ResolveProjectPath(family.ManagedProject, family.Name, "managed_project");
        var nativeProjectPath = ResolveProjectPath(family.NativeProject, family.Name, "native_project");

        _log.Information("Packing family '{0}' at version '{1}'.", family.Name, version);

        // Phase 1: EnsureHarvestReady — gate the pack on a valid ConsolidateHarvest receipt.
        await EnsureHarvestReadyAsync(family, cancellationToken);

        // Phase 2: PrepareMetadata — stamp the native payload with G55 machine-readable metadata.
        await PrepareMetadataAsync(family, version, expectedCommitSha, cancellationToken);

        // Phase 3: PackAndValidate — dotnet pack, normalize cross-family deps, post-pack guardrails.
        await PackAndValidateAsync(family, version, expectedCommitSha, managedProjectPath, nativeProjectPath, cancellationToken);
    }

    private Task EnsureHarvestReadyAsync(PackageFamilyConfig family, CancellationToken cancellationToken)
        => EnsureHarvestOutputReadyAsync(family, cancellationToken);

    private Task PrepareMetadataAsync(
        PackageFamilyConfig family,
        string version,
        string expectedCommitSha,
        CancellationToken cancellationToken)
        => _nativePackageMetadataGenerator.GenerateAsync(family, version, expectedCommitSha, cancellationToken);

    private async Task PackAndValidateAsync(
        PackageFamilyConfig family,
        string version,
        string expectedCommitSha,
        FilePath managedProjectPath,
        FilePath nativeProjectPath,
        CancellationToken cancellationToken)
    {
        var nativePayloadSource = _pathService.GetHarvestLibraryDir(family.LibraryRef);

        // Post-S1 (2026-04-17): within-family dependency is SkiaSharp-style minimum range.
        // No exact-pin CPM plumbing, no per-family MSBuild property. Each pack invocation
        // gets $(Version) and (for the native only) $(NativePayloadSource). Managed's
        // ProjectReference to the native emits as a standard `>=` dependency in the nuspec.
        // Drift protection is orchestration-time: both packs carry identical `version` and
        // the post-pack validator asserts the emitted `<version>` elements match (G23).
        var nativeInvocation = new DotNetPackInvocation(
            Configuration: _dotNetBuildConfiguration.Configuration,
            Version: version,
            NativePayloadSource: nativePayloadSource);

        var managedInvocation = nativeInvocation with { NativePayloadSource = null };

        var nativePackResult = _dotNetPackInvoker.Pack(nativeProjectPath, nativeInvocation, noRestore: false, noBuild: false);
        if (nativePackResult.IsError())
        {
            LogAndThrowPackError(family, nativePackResult.DotNetPackError);
        }

        var managedPackResult = _dotNetPackInvoker.Pack(managedProjectPath, managedInvocation, noRestore: false, noBuild: false);
        if (managedPackResult.IsError())
        {
            LogAndThrowPackError(family, managedPackResult.DotNetPackError);
        }

        var artifacts = CreateArtifacts(family, version);
        await NormalizeCrossFamilyDependencyRangesAsync(family, artifacts.ManagedPackage, version, cancellationToken);

        var metadataResult = await _projectMetadataReader.ReadAsync(managedProjectPath, cancellationToken);
        if (metadataResult.IsError())
        {
            var error = metadataResult.ProjectMetadataError;
            _log.Error("Project metadata resolution failed for family '{0}': {1}", family.Name, error.Message);
            throw new CakeException(
                $"Project metadata resolution failed for family '{family.Name}'. Error: {error.Message}");
        }

        var validationResult = await _packageOutputValidator.ValidateAsync(
            family,
            artifacts,
            version,
            expectedCommitSha,
            metadataResult.ProjectMetadata,
            _manifestConfig,
            _pathService.GetReadmeFile());

        LogValidationDiagnostics(family, validationResult.Validation);

        if (validationResult.IsError())
        {
            throw new CakeException(validationResult.PackageValidationError.Message);
        }

        _log.Information(
            "Packed family '{0}': {1}, {2}, {3}",
            family.Name,
            artifacts.NativePackage.GetFilename().FullPath,
            artifacts.ManagedPackage.GetFilename().FullPath,
            artifacts.ManagedSymbolsPackage.GetFilename().FullPath);
    }

    [SuppressMessage("Design", "MA0051:Method is too long",
        Justification = "G56 normalization intentionally keeps zip read, nuspec parse, range rewrite, and zip update in one linear path for debuggability.")]
    private async Task NormalizeCrossFamilyDependencyRangesAsync(
        PackageFamilyConfig family,
        FilePath managedPackagePath,
        string lowerBoundVersion,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(family);
        ArgumentNullException.ThrowIfNull(managedPackagePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(lowerBoundVersion);

        if (family.DependsOn.Count == 0)
        {
            return;
        }

        if (!_cakeContext.FileExists(managedPackagePath))
        {
            _log.Verbose(
                "Skipping cross-family dependency normalization because managed package '{0}' does not exist yet.",
                managedPackagePath.GetFilename().FullPath);
            return;
        }

        await using var packageStream = _cakeContext.FileSystem
            .GetFile(managedPackagePath)
            .Open(FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        using var archive = new ZipArchive(packageStream, ZipArchiveMode.Update, leaveOpen: false);

        var nuspecEntry = archive.Entries.SingleOrDefault(entry =>
            entry.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase) &&
            !entry.FullName.StartsWith("package/", StringComparison.OrdinalIgnoreCase));

        if (nuspecEntry is null)
        {
            _log.Warning(
                "Managed package '{0}' does not contain a nuspec entry. Skipping cross-family dependency normalization.",
                managedPackagePath.GetFilename().FullPath);
            return;
        }

        string nuspecContent;
        using (var reader = new StreamReader(nuspecEntry.Open(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false))
        {
            nuspecContent = await reader.ReadToEndAsync(cancellationToken);
        }

        var document = XDocument.Parse(nuspecContent, LoadOptions.PreserveWhitespace);
        var root = document.Root;
        if (root is null)
        {
            return;
        }

        var xmlNamespace = root.Name.Namespace;
        var dependencyElements = root
            .Descendants(xmlNamespace + "dependency")
            .ToList();

        var hasChanges = false;
        foreach (var dependencyFamily in family.DependsOn)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var dependencyPackageId = FamilyIdentifierConventions.ManagedPackageId(dependencyFamily);
            var upperBound = ResolveCrossFamilyUpperBound(dependencyFamily);
            var expectedRange = $"[{lowerBoundVersion}, {upperBound})";

            foreach (var dependencyElement in dependencyElements.Where(element =>
                         string.Equals((string?)element.Attribute("id"), dependencyPackageId, StringComparison.OrdinalIgnoreCase)))
            {
                var currentVersion = (string?)dependencyElement.Attribute("version");
                if (string.Equals(currentVersion, expectedRange, StringComparison.Ordinal))
                {
                    continue;
                }

                dependencyElement.SetAttributeValue("version", expectedRange);
                hasChanges = true;
            }
        }

        if (!hasChanges)
        {
            return;
        }

        var nuspecEntryName = nuspecEntry.FullName;
        nuspecEntry.Delete();
        var updatedNuspecEntry = archive.CreateEntry(nuspecEntryName, CompressionLevel.Optimal);

        var updatedContent = document.Declaration is null
            ? document.ToString(SaveOptions.DisableFormatting)
            : string.Concat(document.Declaration, Environment.NewLine, document.ToString(SaveOptions.DisableFormatting));

        await using var updatedEntryStream = updatedNuspecEntry.Open();
        await using var writer = new StreamWriter(updatedEntryStream, new UTF8Encoding(false));
        await writer.WriteAsync(updatedContent);

        _log.Verbose(
            "Normalized cross-family dependency ranges in managed package '{0}' for family '{1}'.",
            managedPackagePath.GetFilename().FullPath,
            family.Name);
    }

    private NuGetVersion ResolveCrossFamilyUpperBound(string dependencyFamily)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dependencyFamily);

        var dependencyFamilyConfig = _manifestConfig.PackageFamilies.SingleOrDefault(candidate =>
            string.Equals(candidate.Name, dependencyFamily, StringComparison.OrdinalIgnoreCase));

        if (dependencyFamilyConfig is null)
        {
            throw new CakeException(
                $"Cannot resolve cross-family dependency range for '{dependencyFamily}' because it does not exist in manifest package_families[].");
        }

        var dependencyLibrary = _manifestConfig.LibraryManifests.SingleOrDefault(candidate =>
            string.Equals(candidate.Name, dependencyFamilyConfig.LibraryRef, StringComparison.OrdinalIgnoreCase));

        if (dependencyLibrary is null)
        {
            throw new CakeException(
                $"Cannot resolve cross-family dependency range for '{dependencyFamily}' because library_ref '{dependencyFamilyConfig.LibraryRef}' does not exist in manifest library_manifests[].");
        }

        if (!NuGetVersion.TryParse(dependencyLibrary.VcpkgVersion, out var upstreamVersion))
        {
            throw new CakeException(
                $"Cannot resolve cross-family dependency range for '{dependencyFamily}' because vcpkg_version '{dependencyLibrary.VcpkgVersion}' is invalid.");
        }

        return new NuGetVersion(upstreamVersion.Major + 1, 0, 0);
    }

    private void LogAndThrowPackError(PackageFamilyConfig family, DotNetPackError error)
    {
        _log.Error("dotnet pack failed for family '{0}': {1}", family.Name, error.Message);
        if (error.Exception is not null)
        {
            _log.Verbose("Details: {0}", error.Exception);
        }

        throw new CakeException($"dotnet pack failed for family '{family.Name}'. Error: {error.Message}");
    }

    private void LogValidationDiagnostics(PackageFamilyConfig family, PackageValidation validation)
    {
        foreach (var check in validation.Checks.Where(check => check.IsError))
        {
            _log.Error(
                "  - [{0}] {1}",
                check.Kind,
                check.ErrorMessage ?? "<no message>");
        }

        if (!validation.HasErrors)
        {
            _log.Verbose(
                "Family '{0}' post-pack validation: {1} check(s) passed.",
                family.Name,
                validation.Checks.Count);
        }
    }

    private async Task EnsureHarvestOutputReadyAsync(PackageFamilyConfig family, CancellationToken cancellationToken)
    {
        var manifestPath = _pathService.GetHarvestLibraryManifestFile(family.LibraryRef);

        if (!_cakeContext.FileExists(manifestPath))
        {
            throw new CakeException(
                $"Package task cannot pack family '{family.Name}' because harvest manifest '{manifestPath.FullPath}' is missing. Run Harvest + ConsolidateHarvest for library '{family.LibraryRef}' first.");
        }

        var harvestManifest = await _cakeContext.ToJsonAsync<HarvestManifest>(manifestPath);
        cancellationToken.ThrowIfCancellationRequested();

        AssertConsolidationReceiptValid(family, manifestPath, harvestManifest);
        AssertPayloadSubtreesPopulated(family);

        var successfulRids = harvestManifest.Rids
            .Where(ridStatus => ridStatus.Success)
            .Select(ridStatus => ridStatus.Rid)
            .OrderBy(rid => rid, StringComparer.OrdinalIgnoreCase);

        _log.Information("Family '{0}' will pack harvest payload for successful RIDs: {1}", family.Name, string.Join(", ", successfulRids));
    }

    private void AssertConsolidationReceiptValid(PackageFamilyConfig family, FilePath manifestPath, HarvestManifest harvestManifest)
    {
        if (harvestManifest.Summary.SuccessfulRids == 0)
        {
            throw new CakeException(
                $"Package task cannot pack family '{family.Name}' because harvest manifest '{manifestPath.FullPath}' reports zero successful RIDs.");
        }

        // H1 gate (G51 partner): harvest-manifest must carry a consolidation receipt and
        // that receipt must declare a non-zero license payload. Absence means either the
        // manifest is pre-H1 legacy (no consolidation section) OR Consolidate was invoked
        // against zero successful RIDs. Either way, Pack cannot proceed — the native csproj
        // reads from licenses/_consolidated/ and would ship a nupkg with no attribution.
        if (harvestManifest.Consolidation is null)
        {
            throw new CakeException(
                $"Package task cannot pack family '{family.Name}' because harvest manifest '{manifestPath.FullPath}' lacks a consolidation receipt. " +
                "Re-run ConsolidateHarvest — Harvest invalidates the consolidated view on every RID run, and only Consolidate regenerates it with the license-union receipt attached.");
        }

        if (!harvestManifest.Consolidation.LicensesConsolidated || harvestManifest.Consolidation.LicenseEntriesCount == 0)
        {
            throw new CakeException(
                $"Package task cannot pack family '{family.Name}' because the consolidation receipt in '{manifestPath.FullPath}' reports zero license entries " +
                $"(LicensesConsolidated={harvestManifest.Consolidation.LicensesConsolidated}, LicenseEntriesCount={harvestManifest.Consolidation.LicenseEntriesCount}). " +
                "A native pack without third-party license attribution breaks the compliance surface — re-run Harvest + ConsolidateHarvest and confirm at least one successful RID contributed license files.");
        }

        if (harvestManifest.Consolidation.DivergentLicenses.Count > 0)
        {
            _log.Warning(
                "Family '{0}' pack will ship {1} divergent license entries across RIDs (auditable via harvest-manifest.json).",
                family.Name,
                harvestManifest.Consolidation.DivergentLicenses.Count);
        }
    }

    /// <summary>
    /// Post-H1 gate: validate the exact subtrees the native csproj consumes
    /// (see <c>src/native/Directory.Build.props</c>). Runtimes lives at the library-level
    /// <c>runtimes/</c> dir; third-party license attribution comes exclusively from
    /// <c>licenses/_consolidated/</c> produced by <c>ConsolidateHarvestTask</c>'s staged
    /// swap. Top-level <c>licenses/</c> is NOT checked — per-RID evidence under
    /// <c>licenses/{rid}/</c> would satisfy a naive parent-level non-empty check while
    /// shipping an empty license payload on the pack side.
    /// </summary>
    private void AssertPayloadSubtreesPopulated(PackageFamilyConfig family)
    {
        var payloadDirectories = new[]
        {
            _pathService.GetHarvestLibraryRuntimesDir(family.LibraryRef),
            _pathService.GetHarvestLibraryConsolidatedLicensesDir(family.LibraryRef),
        };

        foreach (var payloadSource in payloadDirectories)
        {
            if (!_cakeContext.DirectoryExists(payloadSource))
            {
                throw new CakeException(
                    $"Package task cannot pack family '{family.Name}' because harvest payload directory '{payloadSource.FullPath}' is missing.");
            }

            var payloadFiles = _cakeContext.GetFiles($"{payloadSource}/**/*");
            if (payloadFiles.Count == 0)
            {
                throw new CakeException(
                    $"Package task cannot pack family '{family.Name}' because harvest payload directory '{payloadSource.FullPath}' is empty.");
            }
        }
    }

    private PackageArtifacts CreateArtifacts(PackageFamilyConfig family, string version)
    {
        var managedPackageId = FamilyIdentifierConventions.ManagedPackageId(family.Name);
        var nativePackageId = FamilyIdentifierConventions.NativePackageId(family.Name);

        return new PackageArtifacts(
            ManagedPackage: _pathService.PackagesOutput.CombineWithFilePath($"{managedPackageId}.{version}.nupkg"),
            ManagedSymbolsPackage: _pathService.PackagesOutput.CombineWithFilePath($"{managedPackageId}.{version}.snupkg"),
            NativePackage: _pathService.PackagesOutput.CombineWithFilePath($"{nativePackageId}.{version}.nupkg"));
    }

    private FilePath ResolveProjectPath(string? relativePath, string familyName, string manifestFieldName)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new CakeException($"Family '{familyName}' is missing manifest field '{manifestFieldName}'.");
        }

        return _pathService.RepoRoot.CombineWithFilePath(new FilePath(relativePath));
    }

    private string ResolveHeadCommitSha() => _resolveHeadCommitSha();

    private static string DefaultResolveHeadCommitSha(ICakeContext context, DirectoryPath repoRoot)
    {
        var tip = context.GitLogTip(repoRoot);
        if (tip is null || string.IsNullOrWhiteSpace(tip.Sha))
        {
            throw new CakeException(
                $"Package task could not resolve git HEAD commit SHA via Cake.Frosting.Git GitLogTip at '{repoRoot.FullPath}'. " +
                "Ensure the repo root is a valid git checkout.");
        }

        return tip.Sha;
    }
}
