using Build.Application.Harvesting;
using Build.Context;
using Cake.Frosting;

namespace Build.Tasks.Harvest;

[TaskName("Harvest")]
public sealed class HarvestTask(HarvestTaskRunner harvestTaskRunner) : AsyncFrostingTask<BuildContext>
{
    private readonly HarvestTaskRunner _harvestTaskRunner = harvestTaskRunner ?? throw new ArgumentNullException(nameof(harvestTaskRunner));

    public override Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return _harvestTaskRunner.RunAsync(context);
    }
}