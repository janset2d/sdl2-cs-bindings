# pragma warning disable CA1031

using Build.Host;
using Cake.Frosting;

namespace Build.Features.Info;

[TaskName("Info")]
public sealed class InfoTask(InfoPipeline infoPipeline) : AsyncFrostingTask<BuildContext>
{
    private readonly InfoPipeline _infoPipeline = infoPipeline ?? throw new ArgumentNullException(nameof(infoPipeline));

    public override async Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        await _infoPipeline.RunAsync();
    }
}
