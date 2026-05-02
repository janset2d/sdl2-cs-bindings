using Build.Host;
using Cake.Frosting;

namespace Build.Features.Ci;

[TaskName("GenerateMatrix")]
[TaskDescription("Emits artifacts/matrix/runtimes.json — the GitHub-Actions matrix derived from manifest.runtimes[]")]
public sealed class GenerateMatrixTask(GenerateMatrixTaskRunner generateMatrixTaskRunner) : AsyncFrostingTask<BuildContext>
{
    private readonly GenerateMatrixTaskRunner _generateMatrixTaskRunner = generateMatrixTaskRunner ?? throw new ArgumentNullException(nameof(generateMatrixTaskRunner));

    public override Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return _generateMatrixTaskRunner.RunAsync();
    }
}
