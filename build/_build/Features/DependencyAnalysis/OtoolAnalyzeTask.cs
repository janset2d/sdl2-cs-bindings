using Build.Host;
using Cake.Frosting;

namespace Build.Features.DependencyAnalysis;

[TaskName("Otool-Analyze")]
public sealed class OtoolAnalyzeTask : AsyncFrostingTask<BuildContext>
{
    private readonly OtoolAnalyzePipeline _otoolAnalyzePipeline;

    public OtoolAnalyzeTask() : this(new OtoolAnalyzePipeline())
    {
    }

    public OtoolAnalyzeTask(OtoolAnalyzePipeline otoolAnalyzePipeline)
    {
        _otoolAnalyzePipeline = otoolAnalyzePipeline ?? throw new ArgumentNullException(nameof(otoolAnalyzePipeline));
    }

    public override Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return _otoolAnalyzePipeline.RunAsync(context);
    }
}
