using Build.Host;
using Cake.Core;
using Cake.Frosting;

namespace Build.Targets.PublishPublic;

[TaskName("PublishPublic")]
[TaskDescription("Stub — public NuGet.org promotion is not implemented yet.")]
public sealed class PublishPublicTask : AsyncFrostingTask<BuildContext>
{
    public override Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        throw new CakeException("PublishPublic is not implemented yet. Staging-validated artifacts will promote to nuget.org via a separate workflow.");
    }
}
