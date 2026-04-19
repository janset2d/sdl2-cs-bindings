# pragma warning disable CA1031

using Build.Application.Common;
using Build.Context;
using Cake.Frosting;

namespace Build.Tasks.Common;

[TaskName("Info")]
public sealed class InfoTask(InfoTaskRunner infoTaskRunner) : AsyncFrostingTask<BuildContext>
{
    private readonly InfoTaskRunner _infoTaskRunner = infoTaskRunner ?? throw new ArgumentNullException(nameof(infoTaskRunner));

    public override async Task RunAsync(BuildContext context)
    {
        await _infoTaskRunner.RunAsync(context);
    }
}
