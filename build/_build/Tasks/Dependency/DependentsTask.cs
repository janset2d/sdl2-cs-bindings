﻿using Build.Context;
using Build.Tools.Dumpbin;
using Cake.Common.Diagnostics;
using Cake.Common.IO;
using Cake.Frosting;

namespace Build.Tasks.Dependency;

[TaskName("Dumpbin-Dependents")]
public class DependentsTask : AsyncFrostingTask<BuildContext>
{
    public override async Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var file = context.File(context.DumpbinConfiguration.DllToDump[0]);

        if (!context.FileExists(file))
        {
            context.Warning("File not found: {0}", file.Path);
        }

        var dumpbinSettings = new DumpbinDependentsSettings(file)
        {
            ToolPath = context.Tools.Resolve("dumpbin.exe"),
            SetupProcessSettings = settings =>
            {
                settings.RedirectStandardOutput = true;
                settings.RedirectStandardError = true;
            },
        };

        var rawOutput = await Task.Run(() => context.DumpbinDependents(dumpbinSettings) ?? string.Empty).ConfigureAwait(false);

        context.Verbose(rawOutput);
    }
}
