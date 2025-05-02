using Build.Context;
using Build.Tools.Dumpbin;
using Cake.Common.Diagnostics;
using Cake.Frosting;

namespace Build.Tasks.Dumpbin;

[TaskName("Dumpbin-Dependents")]
public class DependentsTask : AsyncFrostingTask<BuildContext>
{
    public override Task RunAsync(BuildContext context)
    {
        context.Information("Dumpbin dependents task started.");
        var dlls = context.DumpbinSettings.DllToDump;

        if (dlls.Count == 0)
        {
            context.Information("No DLLs to dump.");
        }

        foreach (var dll in dlls)
        {
            var dependents = context.DumpbinDependents(new DumpbinDependentsSettings(dll));

            if(dependents == null || dependents.Count == 0)
            {
                context.Information($"No dependents found for {dll}");
                continue;
            }

            foreach (var dependent in dependents)
            {
                context.Information(dependent);
            }
        }

        return Task.CompletedTask;
    }
}
