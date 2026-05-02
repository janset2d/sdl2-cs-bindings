using Build.Host;
using Cake.Frosting;

namespace Build.Features.Maintenance;

[TaskName("CompileSolution")]
[TaskDescription("Runs `dotnet build Janset.SDL2.sln` using the active BuildContext configuration")]
public sealed class CompileSolutionTask(CompileSolutionPipeline compileSolutionPipeline) : AsyncFrostingTask<BuildContext>
{
    private readonly CompileSolutionPipeline _compileSolutionPipeline = compileSolutionPipeline ?? throw new ArgumentNullException(nameof(compileSolutionPipeline));

    public override Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return _compileSolutionPipeline.RunAsync();
    }
}
