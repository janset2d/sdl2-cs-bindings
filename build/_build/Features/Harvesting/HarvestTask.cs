using Build.Host;
using Build.Host.Configuration;
using Cake.Frosting;

namespace Build.Features.Harvesting;

[TaskName("Harvest")]
public sealed class HarvestTask(
    HarvestTaskRunner harvestTaskRunner,
    VcpkgConfiguration vcpkgConfiguration) : AsyncFrostingTask<BuildContext>
{
    private readonly HarvestTaskRunner _harvestTaskRunner = harvestTaskRunner ?? throw new ArgumentNullException(nameof(harvestTaskRunner));
    private readonly VcpkgConfiguration _vcpkgConfiguration = vcpkgConfiguration ?? throw new ArgumentNullException(nameof(vcpkgConfiguration));

    public override Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var request = new HarvestRequest(context.Runtime.Rid, _vcpkgConfiguration.Libraries.ToList());
        return _harvestTaskRunner.RunAsync(context, request);
    }
}
