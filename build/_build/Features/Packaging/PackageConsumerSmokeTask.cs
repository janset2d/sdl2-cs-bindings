using Build.Host;
using Build.Host.Configuration;
using Build.Host.Paths;
using Build.Shared.Runtime;
using Cake.Core;
using Cake.Frosting;

namespace Build.Features.Packaging;

[TaskName("PackageConsumerSmoke")]
[TaskDescription("Restores and runs the D-local package consumer smoke app against artifacts/packages")]
public sealed class PackageConsumerSmokeTask(
    IPackageConsumerSmokePipeline packageConsumerSmokePipeline,
    PackageBuildConfiguration packageBuildConfiguration,
    IRuntimeProfile runtimeProfile,
    IPathService pathService) : AsyncFrostingTask<BuildContext>
{
    private readonly IPackageConsumerSmokePipeline _packageConsumerSmokePipeline = packageConsumerSmokePipeline ?? throw new ArgumentNullException(nameof(packageConsumerSmokePipeline));
    private readonly PackageBuildConfiguration _packageBuildConfiguration = packageBuildConfiguration ?? throw new ArgumentNullException(nameof(packageBuildConfiguration));
    private readonly IRuntimeProfile _runtimeProfile = runtimeProfile ?? throw new ArgumentNullException(nameof(runtimeProfile));
    private readonly IPathService _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));

    public override Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (_packageBuildConfiguration.FamilyVersionMapping.Count == 0)
        {
            throw new CakeException(
                "PackageConsumerSmoke requires --versions-file <path>. " +
                "Run --target ResolveVersions first to produce a versions.json, " +
                "then re-run with --versions-file artifacts/resolve-versions/versions.json.");
        }

        var request = new PackageConsumerSmokeRequest(
            _runtimeProfile.Rid,
            _packageBuildConfiguration.FamilyVersionMapping,
            _pathService.PackagesOutput);

        return _packageConsumerSmokePipeline.RunAsync(request);
    }
}
