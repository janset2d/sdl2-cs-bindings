using Build.Host;
using Cake.Frosting;

namespace Build.Features.Diagnostics;

[TaskName("Inspect-HarvestedDependencies")]
[TaskDescription("Per-RID dep-scan of harvest payload; extracts Unix tarballs, reads Windows native/ directly, then runs Dumpbin/Ldd/Otool")]
public sealed class InspectHarvestedDependenciesTask(InspectHarvestedDependenciesTaskRunner runner) : AsyncFrostingTask<BuildContext>
{
    private readonly InspectHarvestedDependenciesTaskRunner _runner = runner ?? throw new ArgumentNullException(nameof(runner));

    public override Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return _runner.RunAsync(context);
    }
}
