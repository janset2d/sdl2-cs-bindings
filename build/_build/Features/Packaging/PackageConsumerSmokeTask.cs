using Build.Host;
using Build.Host.Configuration;
using Build.Host.Paths;
using Build.Shared.Runtime;
using Cake.Core.Diagnostics;
using Cake.Frosting;

namespace Build.Features.Packaging;

[TaskName("PackageConsumerSmoke")]
[TaskDescription("Restores and runs the D-local package consumer smoke app against artifacts/packages")]
public sealed class PackageConsumerSmokeTask(
    IPackageConsumerSmokePipeline packageConsumerSmokePipeline,
    PackageBuildConfiguration packageBuildConfiguration,
    ICakeLog log,
    IRuntimeProfile runtimeProfile,
    IPathService pathService) : AsyncFrostingTask<BuildContext>
{
    private readonly IPackageConsumerSmokePipeline _packageConsumerSmokePipeline = packageConsumerSmokePipeline ?? throw new ArgumentNullException(nameof(packageConsumerSmokePipeline));
    private readonly PackageBuildConfiguration _packageBuildConfiguration = packageBuildConfiguration ?? throw new ArgumentNullException(nameof(packageBuildConfiguration));
    private readonly ICakeLog _log = log ?? throw new ArgumentNullException(nameof(log));
    private readonly IRuntimeProfile _runtimeProfile = runtimeProfile ?? throw new ArgumentNullException(nameof(runtimeProfile));
    private readonly IPathService _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));

    /// <summary>
    /// Post-C.8 (Deniz Q5a decision, 2026-04-21): PackageConsumerSmoke is only meaningful
    /// when the operator or a CI <c>resolve-versions</c> job output supplies at least one
    /// <c>--explicit-version family=semver</c> entry (or a <c>--versions-file</c> mapping).
    /// When the mapping is empty, the task is auto-skipped with a log hint pointing at the
    /// canonical <c>--explicit-version</c> / <c>--versions-file</c> CLI surface. Mirrors
    /// <c>PackageTask.ShouldRun</c> — same rationale.
    /// </summary>
    public override bool ShouldRun(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (_packageBuildConfiguration.ExplicitVersions.Count > 0)
        {
            return true;
        }

        _log.Information(
            "PackageConsumerSmoke skipped: no --explicit-version / --versions-file mapping supplied. " +
            "Pass --explicit-version <family>=<semver> per concrete family, or --versions-file <path> " +
            "pointing at a serialized mapping (e.g. the artifact emitted by --target ResolveVersions, " +
            "or the resolve-versions job's output threaded by the CI matrix).");
        return false;
    }

    public override Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var request = new PackageConsumerSmokeRequest(
            _runtimeProfile.Rid,
            _packageBuildConfiguration.ExplicitVersions,
            _pathService.PackagesOutput);

        return _packageConsumerSmokePipeline.RunAsync(request);
    }
}
