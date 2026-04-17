using Build.Context;
using Build.Modules.Contracts;
using Build.Tasks.Preflight;
using Cake.Frosting;

namespace Build.Tasks.Packaging;

[TaskName("Package")]
[TaskDescription("Packs managed/native families with explicit version propagation and post-pack nuspec assertions")]
[IsDependentOn(typeof(PreFlightCheckTask))]
public sealed class PackageTask(IPackageTaskRunner packageTaskRunner) : AsyncFrostingTask<BuildContext>
{
    private readonly IPackageTaskRunner _packageTaskRunner = packageTaskRunner ?? throw new ArgumentNullException(nameof(packageTaskRunner));

    public override Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return _packageTaskRunner.RunAsync();
    }
}
