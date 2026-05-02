using Build.Host;
using Build.Host.Configuration;
using Cake.Core.Diagnostics;
using Cake.Frosting;

namespace Build.Features.Packaging;

[TaskName("PackageConsumerSmoke")]
[TaskDescription("Restores and runs the D-local package consumer smoke app against artifacts/packages")]
public sealed class PackageConsumerSmokeTask(
    IPackageConsumerSmokePipeline packageConsumerSmokePipeline,
    PackageBuildConfiguration packageBuildConfiguration,
    ICakeLog log) : AsyncFrostingTask<BuildContext>
{
    private readonly IPackageConsumerSmokePipeline _packageConsumerSmokePipeline = packageConsumerSmokePipeline ?? throw new ArgumentNullException(nameof(packageConsumerSmokePipeline));
    private readonly PackageBuildConfiguration _packageBuildConfiguration = packageBuildConfiguration ?? throw new ArgumentNullException(nameof(packageBuildConfiguration));
    private readonly ICakeLog _log = log ?? throw new ArgumentNullException(nameof(log));

    /// <summary>
    /// Post-C.8 (Deniz Q5a decision, 2026-04-21): PackageConsumerSmoke is only meaningful
    /// when the operator or a CI <c>resolve-versions</c> job output supplies at least one
    /// <c>--explicit-version family=semver</c> entry. When the mapping is empty, the task
    /// is auto-skipped with a log hint pointing at <c>SetupLocalDev --source=local</c> (which
    /// resolves the mapping locally) and at the canonical <c>--explicit-version</c> CLI
    /// surface for ad-hoc invocations. Mirrors <c>PackageTask.ShouldRun</c> — same rationale.
    /// </summary>
    public override bool ShouldRun(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (_packageBuildConfiguration.ExplicitVersions.Count > 0)
        {
            return true;
        }

        _log.Information(
            "PackageConsumerSmoke skipped: no --explicit-version mapping supplied. Run " +
            "'SetupLocalDev --source=local --rid <rid>' to resolve the mapping locally and " +
            "follow up with '--target=PackageConsumerSmoke --explicit-version <family>=<semver>' " +
            "(repeat per concrete family), or invoke via the CI matrix which threads the " +
            "resolve-versions job's emitted mapping into this target.");
        return false;
    }

    public override Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var request = new PackageConsumerSmokeRequest(
            context.Runtime.Rid,
            _packageBuildConfiguration.ExplicitVersions,
            context.Paths.PackagesOutput);

        return _packageConsumerSmokePipeline.RunAsync(context, request);
    }
}
