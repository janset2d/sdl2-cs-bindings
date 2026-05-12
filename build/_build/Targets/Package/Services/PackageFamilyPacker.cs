using Build.Host.Paths;
using Build.Data.Manifest.Models;
using Build.Data.ProjectMetadata;
using Build.Targets.Package.Models;
using Build.Targets.Package.Reporting;
using Build.Validation.Packaging;
using Cake.Common.Tools.DotNet;
using Cake.Common.Tools.DotNet.MSBuild;
using Cake.Common.Tools.DotNet.Pack;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;

namespace Build.Targets.Package.Services;

/// <summary>
/// Per-family pack orchestrator. Owns the 3-phase per-family flow:
/// EnsureHarvestReady → PrepareMetadata → PackAndValidate. Called once per family
/// from <see cref="Build.Targets.Package.PackageTask"/>.
/// </summary>
public sealed class PackageFamilyPacker
{
    private const string NativePayloadSourceProperty = "NativePayloadSource";

    private readonly ICakeContext _cakeContext;
    private readonly ICakeLog _log;
    private readonly IPathService _pathService;
    private readonly INativePackageMetadataGenerator _nativePackageMetadataGenerator;
    private readonly IProjectMetadataReader _projectMetadataReader;
    private readonly IPackageOutputValidator _packageOutputValidator;
    private readonly IHarvestReadinessValidator _harvestReadinessValidator;
    private readonly DependencyRangeNormalizer _dependencyRangeNormalizer;
    private readonly PackageReporter _reporter;

    public PackageFamilyPacker(
        ICakeContext cakeContext,
        ICakeLog log,
        IPathService pathService,
        INativePackageMetadataGenerator nativePackageMetadataGenerator,
        IProjectMetadataReader projectMetadataReader,
        IPackageOutputValidator packageOutputValidator,
        IHarvestReadinessValidator harvestReadinessValidator,
        DependencyRangeNormalizer dependencyRangeNormalizer,
        PackageReporter reporter)
    {
        _cakeContext = cakeContext ?? throw new ArgumentNullException(nameof(cakeContext));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));
        _nativePackageMetadataGenerator = nativePackageMetadataGenerator ?? throw new ArgumentNullException(nameof(nativePackageMetadataGenerator));
        _projectMetadataReader = projectMetadataReader ?? throw new ArgumentNullException(nameof(projectMetadataReader));
        _packageOutputValidator = packageOutputValidator ?? throw new ArgumentNullException(nameof(packageOutputValidator));
        _harvestReadinessValidator = harvestReadinessValidator ?? throw new ArgumentNullException(nameof(harvestReadinessValidator));
        _dependencyRangeNormalizer = dependencyRangeNormalizer ?? throw new ArgumentNullException(nameof(dependencyRangeNormalizer));
        _reporter = reporter ?? throw new ArgumentNullException(nameof(reporter));
    }

    public async Task PackAsync(
        ManifestConfig manifestConfig,
        PackageFamilyConfig family,
        string version,
        string headSha,
        string buildConfiguration,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(manifestConfig);
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
        await _nativePackageMetadataGenerator.GenerateAsync(manifestConfig, family, version, headSha, ct);

        // Phase 3: PackAndValidate — dotnet pack, normalize cross-family deps, post-pack guardrails.
        var nativePayloadSource = _pathService.GetHarvestLibraryDir(family.LibraryRef);

        // Within-family dependency is SkiaSharp-style minimum range. No exact-pin CPM plumbing,
        // no per-family MSBuild property. Each pack invocation gets $(Version) and (for the
        // native only) $(NativePayloadSource). Managed's ProjectReference to the native emits
        // as a standard `>=` dependency in the nuspec. Drift protection is orchestration-time:
        // both packs carry identical `version` and the post-pack validator asserts the emitted
        // <version> elements match (G23).
        try
        {
            PackProject(nativeProjectPath, buildConfiguration, version, nativePayloadSource, noRestore: false, noBuild: false);
            PackProject(managedProjectPath, buildConfiguration, version, nativePayloadSource: null, noRestore: false, noBuild: false);
        }
        catch (CakeException ex)
        {
            _reporter.ReportPackError(family, ex);
            throw new CakeException($"dotnet pack failed for family '{family.Name}'. See log.", ex);
        }

        var artifacts = CreateArtifacts(family, version);
        await _dependencyRangeNormalizer.NormalizeAsync(manifestConfig, family, artifacts.ManagedPackage, version, ct);

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
            manifestConfig,
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

    private void PackProject(
        FilePath projectPath,
        string buildConfiguration,
        string version,
        DirectoryPath? nativePayloadSource,
        bool noRestore,
        bool noBuild)
    {
        var settings = new DotNetPackSettings
        {
            Configuration = buildConfiguration,
            OutputDirectory = _pathService.PackagesOutput,
            NoRestore = noRestore,
            NoBuild = noBuild,
            MSBuildSettings = BuildMSBuildSettings(version, nativePayloadSource),
        };

        _log.Information(
            "Running dotnet pack '{0}' at {1} (noRestore={2}, noBuild={3})",
            projectPath.GetFilename().FullPath,
            version,
            noRestore,
            noBuild);

        _cakeContext.DotNetPack(projectPath.FullPath, settings);
    }

    private static DotNetMSBuildSettings BuildMSBuildSettings(string version, DirectoryPath? nativePayloadSource)
    {
        var settings = new DotNetMSBuildSettings
        {
            Version = version,
        };

        if (nativePayloadSource is not null)
        {
            settings.WithProperty(NativePayloadSourceProperty, nativePayloadSource.FullPath);
        }

        return settings;
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
