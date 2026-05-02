using Build.Host;
using Cake.Frosting;

namespace Build.Features.Maintenance;

[TaskName("CleanArtifacts")]
[TaskDescription("Wipes artifacts/ subtrees + native-smoke build dirs so a fresh Slice-D pass starts clean")]
public sealed class CleanArtifactsTask(CleanArtifactsPipeline cleanArtifactsPipeline) : AsyncFrostingTask<BuildContext>
{
    private readonly CleanArtifactsPipeline _cleanArtifactsPipeline = cleanArtifactsPipeline ?? throw new ArgumentNullException(nameof(cleanArtifactsPipeline));

    public override Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return _cleanArtifactsPipeline.RunAsync();
    }
}
