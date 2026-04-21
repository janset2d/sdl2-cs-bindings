using Build.Application.Packaging;
using Build.Context;
using Build.Context.Configs;
using Cake.Core.Diagnostics;
using Cake.Frosting;

namespace Build.Tasks.Packaging;

[TaskName("Package")]
[TaskDescription("Packs managed/native families with explicit version propagation and post-pack nuspec assertions")]
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
    /// is empty, this task is auto-skipped because the stage boundary is now explicit: local-dev
    /// packaging flows through <c>SetupLocalDev --source=local</c>, and standalone/CI pack
    /// invocations hand the resolved mapping through repeated <c>--explicit-version</c> entries.
    /// <para>
    /// Local-dev packs route through <c>SetupLocalDev --source=local</c>, which resolves a
    /// local-suffixed mapping via <c>ManifestVersionProvider</c> and calls the runner directly
    /// with a populated <see cref="PackageBuildConfiguration"/>. Direct <c>--target Package</c>
    /// without any <c>--explicit-version</c> is a misuse that the skip surfaces visibly in the
    /// Cake log rather than failing opaquely.
    /// </para>
    /// </summary>
    public override bool ShouldRun(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (_packageBuildConfiguration.ExplicitVersions.Count > 0)
        {
            return true;
        }

        _log.Information("Package task skipped: no --explicit-version mapping supplied. Local-dev packaging routes through SetupLocalDev; standalone and CI stage invocations must pass the resolved mapping through --explicit-version.");
        return false;
    }

    public override Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return _packageTaskRunner.RunAsync();
    }
}
