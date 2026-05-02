using Build.Host;
using Cake.Frosting;

namespace Build.Features.Ci;

[TaskName("GenerateMatrix")]
[TaskDescription("Emits artifacts/matrix/runtimes.json — the GitHub-Actions matrix derived from manifest.runtimes[]")]
public sealed class GenerateMatrixTask(GenerateMatrixPipeline generateMatrixPipeline) : AsyncFrostingTask<BuildContext>
{
    private readonly GenerateMatrixPipeline _generateMatrixPipeline = generateMatrixPipeline ?? throw new ArgumentNullException(nameof(generateMatrixPipeline));

    public override Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return _generateMatrixPipeline.RunAsync();
    }
}
