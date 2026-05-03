using Build.Host;
using Build.Host.Configuration;
using Cake.Core.Diagnostics;
using Cake.Frosting;

namespace Build.Features.Packaging;

[TaskName("Package")]
[TaskDescription("Packs managed/native families with explicit version propagation and post-pack nuspec assertions")]
public sealed class PackageTask(
    IPackagePipeline packagePipeline,
    PackageBuildConfiguration packageBuildConfiguration,
    ICakeLog log) : AsyncFrostingTask<BuildContext>
{
    private readonly IPackagePipeline _packagePipeline = packagePipeline ?? throw new ArgumentNullException(nameof(packagePipeline));
    private readonly PackageBuildConfiguration _packageBuildConfiguration = packageBuildConfiguration ?? throw new ArgumentNullException(nameof(packageBuildConfiguration));
    private readonly ICakeLog _log = log ?? throw new ArgumentNullException(nameof(log));

    /// <summary>
    /// Pack is only meaningful when the operator (or a CI <c>resolve-versions</c> job output)
    /// supplies at least one <c>--explicit-version family=semver</c> entry, or a <c>--versions-file</c>
    /// pointing at a serialized mapping. When neither is supplied this task is auto-skipped:
    /// direct <c>--target Package</c> without a resolved mapping is a misuse, and the skip
    /// surfaces it visibly in the Cake log rather than failing opaquely inside the pack.
    /// </summary>
    public override bool ShouldRun(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (_packageBuildConfiguration.ExplicitVersions.Count > 0)
        {
            return true;
        }

        _log.Information("Package task skipped: no --explicit-version / --versions-file mapping supplied. Pass the resolved per-family mapping via --explicit-version <family>=<semver> entries or --versions-file <path>.");
        return false;
    }

    public override Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var request = new PackRequest(_packageBuildConfiguration.ExplicitVersions);
        return _packagePipeline.RunAsync(request);
    }
}
