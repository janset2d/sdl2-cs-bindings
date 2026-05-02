# pragma warning disable CA1031

using Build.Host;
using Cake.Frosting;

namespace Build.Features.Info;

[TaskName("Info")]
public sealed class InfoTask(InfoTaskRunner infoTaskRunner) : AsyncFrostingTask<BuildContext>
{
    private readonly InfoTaskRunner _infoTaskRunner = infoTaskRunner ?? throw new ArgumentNullException(nameof(infoTaskRunner));

    public override async Task RunAsync(BuildContext context)
    {
        await _infoTaskRunner.RunAsync(context);
    }
}
