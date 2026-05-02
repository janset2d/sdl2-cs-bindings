using Build.Host;
using Cake.Frosting;

namespace Build.Features.Harvesting;

[TaskName("NativeSmoke")]
[TaskDescription("Runs native C smoke harness against harvested runtime payload")]
public sealed class NativeSmokeTask(NativeSmokePipeline nativeSmokePipeline) : AsyncFrostingTask<BuildContext>
{
    private readonly NativeSmokePipeline _nativeSmokePipeline = nativeSmokePipeline ?? throw new ArgumentNullException(nameof(nativeSmokePipeline));

    public override Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var request = new NativeSmokeRequest(context.Runtime.Rid);
        return _nativeSmokePipeline.RunAsync(context, request);
    }
}
