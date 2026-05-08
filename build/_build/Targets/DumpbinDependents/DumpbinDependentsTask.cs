using Build.Host;
using Build.Tools.Dumpbin;
using Cake.Common.Diagnostics;
using Cake.Common.IO;
using Cake.Core;
using Cake.Frosting;

namespace Build.Targets.DumpbinDependents;

[TaskName("Dumpbin-Dependents")]
public sealed class DumpbinDependentsTask : AsyncFrostingTask<BuildContext>
{
    public override async Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Dlls.Count == 0)
        {
            throw new CakeException("Dumpbin-Dependents requires --dll <path>. Example: --dll path/to/SDL2.dll");
        }

        var file = context.File(context.Dlls[0]);
        if (!context.FileExists(file))
        {
            context.Warning("File not found: {0}", file.Path);
        }

        var settings = new DumpbinDependentsSettings(file)
        {
            SetupProcessSettings = ps =>
            {
                ps.RedirectStandardOutput = true;
                ps.RedirectStandardError = true;
            },
        };

        var rawOutput = await Task.Run(() => context.DumpbinDependents(settings) ?? string.Empty).ConfigureAwait(false);
        context.Verbose(rawOutput);
    }
}
