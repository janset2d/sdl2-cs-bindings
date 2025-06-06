using Build.Context;
using Build.Tools.Ldd;
using Cake.Common.Diagnostics;
using Cake.Common.IO;
using Cake.Frosting;

namespace Build.Tasks.Dependency;

[TaskName("Ldd-Dependents")]
public class LddTask : AsyncFrostingTask<BuildContext>
{
    public override async Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var file = context.File(context.DumpbinConfiguration.DllToDump[0]);

        if (!context.FileExists(file))
        {
            context.Warning("File not found: {0}", file.Path);
        }

        var settings = new LddSettings(file);
        // var rawOutput = await Task.Run(() => context.Ldd(settings)).ConfigureAwait(false);
        var readOnlyDictionary = await Task.Run(() => context.LddDependencies(settings)).ConfigureAwait(false);

        foreach (var pair in readOnlyDictionary)
        {
            context.Information($"{pair.Key} => {pair.Value}");
        }

        // context.Information(rawOutput);
    }
}
