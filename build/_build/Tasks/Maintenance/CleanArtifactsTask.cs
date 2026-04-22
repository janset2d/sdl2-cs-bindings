using Build.Application.Maintenance;
using Build.Context;
using Cake.Frosting;

namespace Build.Tasks.Maintenance;

[TaskName("CleanArtifacts")]
[TaskDescription("Wipes artifacts/ subtrees + native-smoke build dirs so a fresh Slice-D pass starts clean")]
public sealed class CleanArtifactsTask(CleanArtifactsTaskRunner cleanArtifactsTaskRunner) : AsyncFrostingTask<BuildContext>
{
    private readonly CleanArtifactsTaskRunner _cleanArtifactsTaskRunner = cleanArtifactsTaskRunner ?? throw new ArgumentNullException(nameof(cleanArtifactsTaskRunner));

    public override Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return _cleanArtifactsTaskRunner.RunAsync();
    }
}
