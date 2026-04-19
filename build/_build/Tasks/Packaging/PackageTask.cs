using Build.Application.Packaging;
using Build.Context;
using Build.Context.Configs;
using Build.Tasks.Preflight;
using Cake.Core.Diagnostics;
using Cake.Frosting;

namespace Build.Tasks.Packaging;

[TaskName("Package")]
[TaskDescription("Packs managed/native families with explicit version propagation and post-pack nuspec assertions")]
[IsDependentOn(typeof(PreFlightCheckTask))]
public sealed class PackageTask(
    IPackageTaskRunner packageTaskRunner,
    PackageBuildConfiguration packageBuildConfiguration,
    ICakeLog log) : AsyncFrostingTask<BuildContext>
{
    private readonly IPackageTaskRunner _packageTaskRunner = packageTaskRunner ?? throw new ArgumentNullException(nameof(packageTaskRunner));
    private readonly PackageBuildConfiguration _packageBuildConfiguration = packageBuildConfiguration ?? throw new ArgumentNullException(nameof(packageBuildConfiguration));
    private readonly ICakeLog _log = log ?? throw new ArgumentNullException(nameof(log));

    /// <summary>
    /// Pack is only meaningful when an explicit <c>--family-version</c> is supplied (PostFlight
    /// release chain or PD-8 manual-escape-hatch path). When the flag is absent, this task is
    /// auto-skipped — the common local-dev case runs <c>SetupLocalDev</c> beforehand (which
    /// produces the feed + <c>Janset.Smoke.local.props</c> on its own code path), then calls
    /// <c>PackageConsumerSmoke</c> directly. With ConsumerSmoke still declaring a transitive
    /// dependency on this task, the skip here is what lets the standalone smoke invocation
    /// reach its runner without triggering a duplicate pack.
    ///
    /// Silent skip trade-off: a user invoking <c>--target Package</c> directly without
    /// <c>--family-version</c> sees the task skipped rather than an explicit error. This is
    /// acceptable because the guidance path for local packs is <c>SetupLocalDev --source=local</c>;
    /// release-path <c>PostFlight</c> always provides the flag and never hits this skip.
    /// See phase-2-adaptation-plan.md PD-13 for the broader flag-retirement review.
    /// </summary>
    public override bool ShouldRun(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!string.IsNullOrWhiteSpace(_packageBuildConfiguration.FamilyVersion))
        {
            return true;
        }

        _log.Information("Package task skipped: no --family-version supplied. Local-dev packs route through SetupLocalDev; PostFlight release chain always supplies the flag.");
        return false;
    }

    public override Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return _packageTaskRunner.RunAsync();
    }
}
