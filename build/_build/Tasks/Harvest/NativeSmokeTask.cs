using Build.Application.Harvesting;
using Build.Context;
using Cake.Frosting;

namespace Build.Tasks.Harvest;

[TaskName("NativeSmoke")]
[TaskDescription("Runs native C smoke harness against harvested runtime payload")]
[IsDependentOn(typeof(HarvestTask))]
public sealed class NativeSmokeTask(NativeSmokeTaskRunner nativeSmokeTaskRunner) : AsyncFrostingTask<BuildContext>
{
    private readonly NativeSmokeTaskRunner _nativeSmokeTaskRunner = nativeSmokeTaskRunner ?? throw new ArgumentNullException(nameof(nativeSmokeTaskRunner));

    public override Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return _nativeSmokeTaskRunner.RunAsync(context);
    }
}
