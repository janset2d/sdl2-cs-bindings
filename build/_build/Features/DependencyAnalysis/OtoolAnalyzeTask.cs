using Build.Host;
using Cake.Frosting;

namespace Build.Features.DependencyAnalysis;

[TaskName("Otool-Analyze")]
public sealed class OtoolAnalyzeTask : AsyncFrostingTask<BuildContext>
{
    private readonly OtoolAnalyzeTaskRunner _otoolAnalyzeTaskRunner;

    public OtoolAnalyzeTask() : this(new OtoolAnalyzeTaskRunner())
    {
    }

    public OtoolAnalyzeTask(OtoolAnalyzeTaskRunner otoolAnalyzeTaskRunner)
    {
        _otoolAnalyzeTaskRunner = otoolAnalyzeTaskRunner ?? throw new ArgumentNullException(nameof(otoolAnalyzeTaskRunner));
    }

    public override Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return _otoolAnalyzeTaskRunner.RunAsync(context);
    }
}
