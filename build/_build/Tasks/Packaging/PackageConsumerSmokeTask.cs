using Build.Application.Packaging;
using Build.Context;
using Cake.Frosting;

namespace Build.Tasks.Packaging;

[TaskName("PackageConsumerSmoke")]
[TaskDescription("Restores and runs the D-local package consumer smoke app against artifacts/packages")]
[IsDependentOn(typeof(PackageTask))]
public sealed class PackageConsumerSmokeTask(IPackageConsumerSmokeRunner packageConsumerSmokeRunner) : AsyncFrostingTask<BuildContext>
{
    private readonly IPackageConsumerSmokeRunner _packageConsumerSmokeRunner = packageConsumerSmokeRunner ?? throw new ArgumentNullException(nameof(packageConsumerSmokeRunner));

    public override Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return _packageConsumerSmokeRunner.RunAsync();
    }
}
