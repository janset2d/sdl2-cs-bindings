using Build.Context;
using Cake.Core;
using Cake.Frosting;

namespace Build.Tasks.Publishing;

[TaskName("PublishPublic")]
[TaskDescription("Stub — public NuGet.org promotion is Phase 2b PD-7 work.")]
public sealed class PublishPublicTask : AsyncFrostingTask<BuildContext>
{
    public override Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        throw new CakeException(
            "PublishPublic is not implemented yet. Staging-validated artifacts will promote to nuget.org via a separate workflow with API-key auth; PD-7 covers the orchestration design.");
    }
}
