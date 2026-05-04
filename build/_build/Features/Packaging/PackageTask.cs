using Build.Host;
using Build.Host.Configuration;
using Cake.Core;
using Cake.Frosting;

namespace Build.Features.Packaging;

[TaskName("Package")]
[TaskDescription("Packs managed/native families with explicit version propagation and post-pack nuspec assertions")]
public sealed class PackageTask(
    IPackagePipeline packagePipeline,
    PackageBuildConfiguration packageBuildConfiguration) : AsyncFrostingTask<BuildContext>
{
    private readonly IPackagePipeline _packagePipeline = packagePipeline ?? throw new ArgumentNullException(nameof(packagePipeline));
    private readonly PackageBuildConfiguration _packageBuildConfiguration = packageBuildConfiguration ?? throw new ArgumentNullException(nameof(packageBuildConfiguration));

    public override Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (_packageBuildConfiguration.FamilyVersionMapping.Count == 0)
        {
            throw new CakeException(
                "PackageTask requires --versions-file <path>. " +
                "Run --target ResolveVersions first to produce a versions.json " +
                "(e.g. --target ResolveVersions --version-source=manifest --suffix=local.<timestamp>), " +
                "then re-run with --versions-file artifacts/resolve-versions/versions.json.");
        }

        var request = new PackRequest(_packageBuildConfiguration.FamilyVersionMapping);
        return _packagePipeline.RunAsync(request);
    }
}
