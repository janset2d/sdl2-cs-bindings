using Build.Application.Maintenance;
using Build.Context;
using Cake.Frosting;

namespace Build.Tasks.Maintenance;

[TaskName("CompileSolution")]
[TaskDescription("Runs `dotnet build Janset.SDL2.sln` using the active BuildContext configuration")]
public sealed class CompileSolutionTask(CompileSolutionTaskRunner compileSolutionTaskRunner) : AsyncFrostingTask<BuildContext>
{
    private readonly CompileSolutionTaskRunner _compileSolutionTaskRunner = compileSolutionTaskRunner ?? throw new ArgumentNullException(nameof(compileSolutionTaskRunner));

    public override Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return _compileSolutionTaskRunner.RunAsync();
    }
}
