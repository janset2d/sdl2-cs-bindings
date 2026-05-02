using Build.Host;
using Cake.Frosting;

namespace Build.Features.LocalDev;

[TaskName("SetupLocalDev")]
[TaskDescription("Prepares local package feed and writes build/msbuild/Janset.Local.props for IDE-ready smoke restore/build")]
public sealed class SetupLocalDevTask(SetupLocalDevTaskRunner setupLocalDevTaskRunner) : AsyncFrostingTask<BuildContext>
{
    private readonly SetupLocalDevTaskRunner _setupLocalDevTaskRunner = setupLocalDevTaskRunner ?? throw new ArgumentNullException(nameof(setupLocalDevTaskRunner));

    public override async Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        await _setupLocalDevTaskRunner.RunAsync(context);
    }
}
