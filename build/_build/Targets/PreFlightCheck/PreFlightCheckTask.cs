using Build.Data.Manifest;
using Build.Data.Versions;
using Build.Host;
using Build.Targets.PreFlightCheck.Reporting;
using Build.Validation.Manifest;
using Build.Validation.Packaging;
using Build.Validation.Versioning;
using Cake.Core;
using Cake.Frosting;

namespace Build.Targets.PreFlightCheck;

/// <summary>
/// Cross-cutting validation gate. Loads manifest + vcpkg manifest + resolved versions
/// via repositories, runs every build-host validator, reports findings, and throws once
/// at the boundary if any validator flags an error. Owns orchestration directly — no
/// pipeline class.
/// </summary>
[TaskName("PreFlightCheck")]
[TaskDescription("Validates manifest+vcpkg consistency, hybrid-static overlay coherence, core identity, family name invariants, csproj pack contract, upstream version alignment, and cross-family dependency resolvability before any build operation.")]
public sealed class PreFlightCheckTask(
    IManifestRepository manifestRepository,
    IVcpkgManifestRepository vcpkgManifestRepository,
    IVersionFileRepository versionFileRepository,
    IVersionConsistencyValidator versionConsistencyValidator,
    IHybridStaticOverlayValidator hybridStaticOverlayValidator,
    ICoreLibraryIdentityValidator coreLibraryIdentityValidator,
    IManifestFamilyNameInvariantValidator manifestFamilyNameInvariantValidator,
    ICsprojPackContractValidator csprojPackContractValidator,
    IUpstreamVersionAlignmentValidator upstreamVersionAlignmentValidator,
    ICrossFamilyDependencyResolvabilityValidator crossFamilyDependencyResolvabilityValidator,
    PreflightReporter reporter)
    : AsyncFrostingTask<BuildContext>
{
    private readonly IManifestRepository _manifestRepository = manifestRepository ?? throw new ArgumentNullException(nameof(manifestRepository));
    private readonly IVcpkgManifestRepository _vcpkgManifestRepository = vcpkgManifestRepository ?? throw new ArgumentNullException(nameof(vcpkgManifestRepository));
    private readonly IVersionFileRepository _versionFileRepository = versionFileRepository ?? throw new ArgumentNullException(nameof(versionFileRepository));
    private readonly IVersionConsistencyValidator _versionConsistencyValidator = versionConsistencyValidator ?? throw new ArgumentNullException(nameof(versionConsistencyValidator));
    private readonly IHybridStaticOverlayValidator _hybridStaticOverlayValidator = hybridStaticOverlayValidator ?? throw new ArgumentNullException(nameof(hybridStaticOverlayValidator));
    private readonly ICoreLibraryIdentityValidator _coreLibraryIdentityValidator = coreLibraryIdentityValidator ?? throw new ArgumentNullException(nameof(coreLibraryIdentityValidator));
    private readonly IManifestFamilyNameInvariantValidator _manifestFamilyNameInvariantValidator = manifestFamilyNameInvariantValidator ?? throw new ArgumentNullException(nameof(manifestFamilyNameInvariantValidator));
    private readonly ICsprojPackContractValidator _csprojPackContractValidator = csprojPackContractValidator ?? throw new ArgumentNullException(nameof(csprojPackContractValidator));
    private readonly IUpstreamVersionAlignmentValidator _upstreamVersionAlignmentValidator = upstreamVersionAlignmentValidator ?? throw new ArgumentNullException(nameof(upstreamVersionAlignmentValidator));
    private readonly ICrossFamilyDependencyResolvabilityValidator _crossFamilyDependencyResolvabilityValidator = crossFamilyDependencyResolvabilityValidator ?? throw new ArgumentNullException(nameof(crossFamilyDependencyResolvabilityValidator));
    private readonly PreflightReporter _reporter = reporter ?? throw new ArgumentNullException(nameof(reporter));

    public override Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.VersionsFilePath is null)
        {
            throw new CakeException(
                "PreFlightCheck requires --versions-file <path>. " +
                "Run --target ResolveVersionsFromManifest first to produce a versions.json, " +
                "then re-run with --versions-file artifacts/resolve-versions/versions.json.");
        }

        var manifest = _manifestRepository.Load();
        var vcpkgManifest = _vcpkgManifestRepository.Load();
        var versions = _versionFileRepository.Load(context.VersionsFilePath);

        if (versions.Count == 0)
        {
            throw new CakeException(
                "PreFlightCheck requires a non-empty version mapping. " +
                "Re-run --target ResolveVersionsFromManifest or --target ResolveVersionsFromExplicit.");
        }

        _reporter.ReportRunStart();

        var versionConsistency       = _versionConsistencyValidator.Validate(manifest, vcpkgManifest);
        var hybridStaticOverlay      = _hybridStaticOverlayValidator.Validate(manifest.Runtimes);
        var coreLibraryIdentity      = _coreLibraryIdentityValidator.Validate(manifest);
        var manifestFamilyName       = _manifestFamilyNameInvariantValidator.Validate(manifest);
        var csprojPackContract       = _csprojPackContractValidator.Validate(manifest, context.Paths.RepoRoot);

        var upstreamVersionAlignment = _upstreamVersionAlignmentValidator.Validate(manifest, versions);
        var crossFamilyDependency    = _crossFamilyDependencyResolvabilityValidator.Validate(versions, manifest);

        _reporter.ReportVersionConsistency(versionConsistency);
        _reporter.ReportHybridStaticOverlay(hybridStaticOverlay);
        _reporter.ReportCoreLibraryIdentity(coreLibraryIdentity);
        _reporter.ReportManifestFamilyNameInvariant(manifestFamilyName);
        _reporter.ReportCsprojPackContract(csprojPackContract);
        _reporter.ReportUpstreamVersionAlignment(upstreamVersionAlignment);
        _reporter.ReportCrossFamilyDependencyResolvability(crossFamilyDependency);

        var fatal =
            versionConsistency.HasErrors
            || !hybridStaticOverlay.IsValid
            || coreLibraryIdentity.HasErrors
            || !manifestFamilyName.IsValid
            || csprojPackContract.HasErrors
            || upstreamVersionAlignment.HasErrors
            || crossFamilyDependency.HasErrors;

        if (fatal)
        {
            throw new CakeException(
                "Pre-flight check failed. Review the errors above and fix manifest.json / vcpkg.json / csproj files.");
        }

        return Task.CompletedTask;
    }
}
