using Build.Host;
using Build.Tools.Ldd;
using Cake.Common.Diagnostics;
using Cake.Common.IO;
using Cake.Core;
using Cake.Frosting;

namespace Build.Targets.LddDependents;

[TaskName("Ldd-Dependents")]
public sealed class LddDependentsTask : AsyncFrostingTask<BuildContext>
{
    public override async Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Dlls.Count == 0)
        {
            throw new CakeException("Ldd-Dependents requires --dll <path>. Example: --dll path/to/libSDL2.so");
        }

        var file = context.File(context.Dlls[0]);
        if (!context.FileExists(file))
        {
            context.Warning("File not found: {0}", file.Path);
        }

        var settings = new LddSettings(file);
        var deps = await Task.Run(() => context.LddDependencies(settings)).ConfigureAwait(false);

        foreach (var pair in deps)
        {
            context.Information($"{pair.Key} => {pair.Value}");
        }
    }
}
