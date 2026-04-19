using Build.Application.Harvesting;
using Build.Context;
using Cake.Frosting;

namespace Build.Tasks.Harvest;

[TaskName("ConsolidateHarvest")]
[IsDependentOn(typeof(HarvestTask))]
public sealed class ConsolidateHarvestTask : AsyncFrostingTask<BuildContext>
{
    private readonly ConsolidateHarvestTaskRunner _consolidateHarvestTaskRunner;

    public ConsolidateHarvestTask() : this(new ConsolidateHarvestTaskRunner())
    {
    }

    public ConsolidateHarvestTask(ConsolidateHarvestTaskRunner consolidateHarvestTaskRunner)
    {
        _consolidateHarvestTaskRunner = consolidateHarvestTaskRunner ?? throw new ArgumentNullException(nameof(consolidateHarvestTaskRunner));
    }

    public override Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return _consolidateHarvestTaskRunner.RunAsync(context);
    }
}