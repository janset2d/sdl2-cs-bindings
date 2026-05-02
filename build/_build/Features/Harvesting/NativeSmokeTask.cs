using Build.Host;
using Cake.Frosting;

namespace Build.Features.Harvesting;

[TaskName("NativeSmoke")]
[TaskDescription("Runs native C smoke harness against harvested runtime payload")]
public sealed class NativeSmokeTask(NativeSmokeTaskRunner nativeSmokeTaskRunner) : AsyncFrostingTask<BuildContext>
{
    private readonly NativeSmokeTaskRunner _nativeSmokeTaskRunner = nativeSmokeTaskRunner ?? throw new ArgumentNullException(nameof(nativeSmokeTaskRunner));

    public override Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var request = new NativeSmokeRequest(context.Runtime.Rid);
        return _nativeSmokeTaskRunner.RunAsync(context, request);
    }
}
