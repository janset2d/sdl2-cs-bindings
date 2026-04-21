using Build.Application.Harvesting;
using Build.Context;
using Build.Domain.Harvesting.Models;
using Cake.Frosting;

namespace Build.Tasks.Harvest;

[TaskName("ConsolidateHarvest")]
public sealed class ConsolidateHarvestTask(ConsolidateHarvestTaskRunner consolidateHarvestTaskRunner) : AsyncFrostingTask<BuildContext>
{
    private readonly ConsolidateHarvestTaskRunner _consolidateHarvestTaskRunner = consolidateHarvestTaskRunner ?? throw new ArgumentNullException(nameof(consolidateHarvestTaskRunner));

    public override Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return _consolidateHarvestTaskRunner.RunAsync(context, new ConsolidateHarvestRequest());
    }
}
