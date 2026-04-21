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
    /// Pack is only meaningful when the operator (or a CI <c>resolve-versions</c> job output)
    /// supplies at least one <c>--explicit-version family=semver</c> entry. When the mapping
    /// is empty, this task is auto-skipped so standalone <c>PackageConsumerSmoke</c> invocations
    /// that transit Package via the legacy <c>[IsDependentOn]</c> chain can reach their runner
    /// without triggering a duplicate pack. The graph-flattening in Slice B2 retires that
    /// transit dependency; the skip here preserves B1-era behaviour until then.
    /// <para>
    /// Local-dev packs route through <c>SetupLocalDev --source=local</c>, which resolves a
    /// local-suffixed mapping via <c>ManifestVersionProvider</c> and calls the runner directly
    /// with a populated <see cref="PackageBuildConfiguration"/> (never hitting this task).
    /// Direct <c>--target Package</c> without any <c>--explicit-version</c> is a misuse that
    /// the skip surfaces visibly in the Cake log rather than failing opaquely.
    /// </para>
    /// </summary>
    public override bool ShouldRun(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (_packageBuildConfiguration.ExplicitVersions.Count > 0)
        {
            return true;
        }

        _log.Information("Package task skipped: no --explicit-version mapping supplied. Local-dev packs route through SetupLocalDev; CI resolves versions via the ResolveVersions target and passes them through --explicit-version.");
        return false;
    }

    public override Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return _packageTaskRunner.RunAsync();
    }
}
