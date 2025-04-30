using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Frosting;

namespace Build;

internal static class Program
{
    public static int Main(string[] args)
    {
        return new CakeHost()
            .UseContext<BuildContext>()
            .Run(args);
    }
}

public sealed class BuildContext : FrostingContext
{
    public bool Delay { get; set; }

    public BuildContext(ICakeContext context)
        : base(context)
    {
        Delay = context.Arguments.HasArgument("delay");
    }
}

[TaskName("Hello")]
public sealed class HelloTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        context.Log.Information("Hello");
    }
}

[TaskName("World")]
[IsDependentOn(typeof(HelloTask))]
public sealed class WorldTask : AsyncFrostingTask<BuildContext>
{
    // Tasks can be asynchronous
    public override async Task RunAsync(BuildContext context)
    {
        if (context.Delay)
        {
            context.Log.Information("Waiting...");
            await Task.Delay(1500);
        }

        context.Log.Information("World");
    }
}

[TaskName("Default")]
[IsDependentOn(typeof(WorldTask))]
public sealed class DefaultTask : FrostingTask
{
}
