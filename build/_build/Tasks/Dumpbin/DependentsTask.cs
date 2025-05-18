using Build.Context;
using Build.Modules.DependencyAnalysis;
using Cake.Common.Diagnostics;
using Cake.Common.IO;
using Cake.Frosting;

namespace Build.Tasks.Dumpbin;

[TaskName("Dumpbin-Dependents")]
public class DependentsTask : AsyncFrostingTask<BuildContext>
{
    public override async Task RunAsync(BuildContext context)
    {
        var windowsDumpbinScanner = new WindowsDumpbinScanner(context);

        var file = context.File(context.DumpbinConfiguration.DllToDump[0]);

        var readOnlySet = await windowsDumpbinScanner.ScanAsync(file, CancellationToken.None);

        foreach (var path in readOnlySet)
        {
            context.Information(path.FullPath);
        }
    }
}
