using Build.Host;
using Cake.Frosting;

namespace Build.Features.DependencyAnalysis;

[TaskName("Otool-Analyze")]
public sealed class OtoolAnalyzeTask(OtoolAnalyzePipeline otoolAnalyzePipeline) : AsyncFrostingTask<BuildContext>
{
    private readonly OtoolAnalyzePipeline _otoolAnalyzePipeline = otoolAnalyzePipeline ?? throw new ArgumentNullException(nameof(otoolAnalyzePipeline));

    public override Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return _otoolAnalyzePipeline.RunAsync();
    }
}
