using Build.Host;
using Cake.Frosting;

namespace Build.Features.LocalDev;

[TaskName("SetupLocalDev")]
[TaskDescription("Prepares local package feed and writes build/msbuild/Janset.Local.props for IDE-ready smoke restore/build")]
public sealed class SetupLocalDevTask(SetupLocalDevFlow setupLocalDevFlow) : AsyncFrostingTask<BuildContext>
{
    private readonly SetupLocalDevFlow _setupLocalDevFlow = setupLocalDevFlow ?? throw new ArgumentNullException(nameof(setupLocalDevFlow));

    public override async Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        await _setupLocalDevFlow.RunAsync(context);
    }
}
