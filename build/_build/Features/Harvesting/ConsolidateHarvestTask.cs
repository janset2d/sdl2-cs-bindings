using Build.Host;
using Cake.Frosting;

namespace Build.Features.Harvesting;

[TaskName("ConsolidateHarvest")]
public sealed class ConsolidateHarvestTask(ConsolidateHarvestPipeline consolidateHarvestPipeline) : AsyncFrostingTask<BuildContext>
{
    private readonly ConsolidateHarvestPipeline _consolidateHarvestPipeline = consolidateHarvestPipeline ?? throw new ArgumentNullException(nameof(consolidateHarvestPipeline));

    public override Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return _consolidateHarvestPipeline.RunAsync(new ConsolidateHarvestRequest());
    }
}
