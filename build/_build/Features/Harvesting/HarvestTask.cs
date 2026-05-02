using Build.Host;
using Build.Host.Configuration;
using Cake.Frosting;

namespace Build.Features.Harvesting;

[TaskName("Harvest")]
public sealed class HarvestTask(
    HarvestPipeline harvestPipeline,
    VcpkgConfiguration vcpkgConfiguration) : AsyncFrostingTask<BuildContext>
{
    private readonly HarvestPipeline _harvestPipeline = harvestPipeline ?? throw new ArgumentNullException(nameof(harvestPipeline));
    private readonly VcpkgConfiguration _vcpkgConfiguration = vcpkgConfiguration ?? throw new ArgumentNullException(nameof(vcpkgConfiguration));

    public override Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var request = new HarvestRequest(context.Runtime.Rid, _vcpkgConfiguration.Libraries.ToList());
        return _harvestPipeline.RunAsync(context, request);
    }
}
