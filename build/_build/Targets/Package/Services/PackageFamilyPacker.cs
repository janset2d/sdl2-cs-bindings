using Build.Host.Paths;
using Build.Manifest;
using Build.Packaging;
using Build.Results;
using Build.Targets.Package.Models;
using Build.Targets.Package.Reporting;
using Build.Validation.Packaging;
using Cake.Core;
using Cake.Core.IO;
// Cake.Core is required for CakeException; kept even though ICakeContext is not a Packer dep.

namespace Build.Targets.Package.Services;

/// <summary>
/// Per-family pack orchestrator. Owns the 3-phase per-family flow:
/// EnsureHarvestReady → PrepareMetadata → PackAndValidate. Called once per family
/// from <see cref="Build.Targets.Package.PackageTask"/>.
/// </summary>
public sealed class PackageFamilyPacker
{
    private readonly IPathService _pathService;
    private readonly ManifestConfig _manifestConfig;
    private readonly IDotNetPackInvoker _dotNetPackInvoker;
    private readonly INativePackageMetadataGenerator _nativePackageMetadataGenerator;
    private readonly IProjectMetadataReader _projectMetadataReader;
    private readonly IPackageOutputValidator _packageOutputValidator;
    private readonly IHarvestReadinessValidator _harvestReadinessValidator;
    private readonly DependencyRangeNormalizer _dependencyRangeNormalizer;
    private readonly PackageReporter _reporter;

    public PackageFamilyPacker(
        IPathService pathService,
        ManifestConfig manifestConfig,
        IDotNetPackInvoker dotNetPackInvoker,
        INativePackageMetadataGenerator nativePackageMetadataGenerator,
        IProjectMetadataReader projectMetadataReader,
        IPackageOutputValidator packageOutputValidator,
        IHarvestReadinessValidator harvestReadinessValidator,
        DependencyRangeNormalizer dependencyRangeNormalizer,
        PackageReporter reporter)
    {
        _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));
        _manifestConfig = manifestConfig ?? throw new ArgumentNullException(nameof(manifestConfig));
        _dotNetPackInvoker = dotNetPackInvoker ?? throw new ArgumentNullException(nameof(dotNetPackInvoker));
        _nativePackageMetadataGenerator = nativePackageMetadataGenerator ?? throw new ArgumentNullException(nameof(nativePackageMetadataGenerator));
        _projectMetadataReader = projectMetadataReader ?? throw new ArgumentNullException(nameof(projectMetadataReader));
        _packageOutputValidator = packageOutputValidator ?? throw new ArgumentNullException(nameof(packageOutputValidator));
        _harvestReadinessValidator = harvestReadinessValidator ?? throw new ArgumentNullException(nameof(harvestReadinessValidator));
        _dependencyRangeNormalizer = dependencyRangeNormalizer ?? throw new ArgumentNullException(nameof(dependencyRangeNormalizer));
        _reporter = reporter ?? throw new ArgumentNullException(nameof(reporter));
    }

    public async Task PackAsync(PackageFamilyConfig family, string version, string headSha, string buildConfiguration, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(family);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentException.ThrowIfNullOrWhiteSpace(headSha);
        ArgumentException.ThrowIfNullOrWhiteSpace(buildConfiguration);

        var managedProjectPath = ResolveProjectPath(family.ManagedProject, family.Name, "managed_project");
        var nativeProjectPath = ResolveProjectPath(family.NativeProject, family.Name, "native_project");

        _reporter.LogPackingFamily(family.Name, version);

        // Phase 1: EnsureHarvestReady — gate the pack on a valid ConsolidateHarvest receipt.
        await _harvestReadinessValidator.EnsureReadyAsync(family, ct);

        // Phase 2: PrepareMetadata — stamp the native payload with G55 machine-readable metadata.
        await _nativePackageMetadataGenerator.GenerateAsync(family, version, headSha, ct);

        // Phase 3: PackAndValidate — dotnet pack, normalize cross-family deps, post-pack guardrails.
        var nativePayloadSource = _pathService.GetHarvestLibraryDir(family.LibraryRef);

        // Within-family dependency is SkiaSharp-style minimum range. No exact-pin CPM plumbing,
        // no per-family MSBuild property. Each pack invocation gets $(Version) and (for the
        // native only) $(NativePayloadSource). Managed's ProjectReference to the native emits
        // as a standard `>=` dependency in the nuspec. Drift protection is orchestration-time:
        // both packs carry identical `version` and the post-pack validator asserts the emitted
        // <version> elements match (G23).
        var nativeInvocation = new DotNetPackInvocation(
            Configuration: buildConfiguration,
            Version: version,
            NativePayloadSource: nativePayloadSource);

        var managedInvocation = nativeInvocation with { NativePayloadSource = null };

        var nativePackResult = _dotNetPackInvoker.Pack(nativeProjectPath, nativeInvocation, noRestore: false, noBuild: false);
        ThrowIfPackFailed(family, nativePackResult);

        var managedPackResult = _dotNetPackInvoker.Pack(managedProjectPath, managedInvocation, noRestore: false, noBuild: false);
        ThrowIfPackFailed(family, managedPackResult);

        var artifacts = CreateArtifacts(family, version);
        await _dependencyRangeNormalizer.NormalizeAsync(family, artifacts.ManagedPackage, version, ct);

        var metadataResult = _projectMetadataReader.Read(managedProjectPath);
        if (metadataResult.IsFailure)
        {
            _reporter.ReportProjectMetadataError(family, metadataResult.Error);
            throw new CakeException($"Project metadata resolution failed for family '{family.Name}'. See log.");
        }

        var report = await _packageOutputValidator.ValidateAsync(
            family,
            artifacts,
            version,
            headSha,
            metadataResult.Value,
            _manifestConfig,
            _pathService.GetReadmeFile());

        _reporter.ReportValidationDiagnostics(family, report);

        if (!report.IsValid)
        {
            throw new CakeException(
                $"Family '{family.Name}' post-pack validation failed with {report.Errors.Count} error(s). See log.");
        }

        _reporter.LogPackedFamily(family, artifacts);
    }

    private FilePath ResolveProjectPath(string? relativePath, string familyName, string manifestFieldName)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new CakeException($"Family '{familyName}' is missing manifest field '{manifestFieldName}'.");
        }

        return _pathService.RepoRoot.CombineWithFilePath(new FilePath(relativePath));
    }

    private void ThrowIfPackFailed(PackageFamilyConfig family, Result<Unit, DotNetPackError> packResult)
    {
        if (!packResult.IsFailure)
        {
            return;
        }

        // Reporter logs the detail; CakeException carries an anchor to send operators to the log.
        _reporter.ReportPackError(family, packResult.Error);
        throw new CakeException($"dotnet pack failed for family '{family.Name}'. See log.");
    }

    private PackageArtifacts CreateArtifacts(PackageFamilyConfig family, string version)
    {
        var managedPackageId = Build.Validation.Conventions.FamilyIdentifierConventions.ManagedPackageId(family.Name);
        var nativePackageId = Build.Validation.Conventions.FamilyIdentifierConventions.NativePackageId(family.Name);

        return new PackageArtifacts(
            ManagedPackage: _pathService.PackagesOutput.CombineWithFilePath($"{managedPackageId}.{version}.nupkg"),
            ManagedSymbolsPackage: _pathService.PackagesOutput.CombineWithFilePath($"{managedPackageId}.{version}.snupkg"),
            NativePackage: _pathService.PackagesOutput.CombineWithFilePath($"{nativePackageId}.{version}.nupkg"));
    }
}
