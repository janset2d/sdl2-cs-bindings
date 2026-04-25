using Build.Application.Versioning;
using Build.Context;
using Cake.Frosting;

namespace Build.Tasks.Versioning;

/// <summary>
/// CI entrypoint for build-host version resolution.
/// Dispatches on <c>--version-source</c>, resolves a family/version mapping inside the
/// build host, and writes <c>artifacts/resolve-versions/versions.json</c> for downstream
/// jobs and local inspection.
/// </summary>
[TaskName("ResolveVersions")]
[TaskDescription("Resolves per-family versions and emits artifacts/resolve-versions/versions.json")]
public sealed class ResolveVersionsTask(ResolveVersionsTaskRunner resolveVersionsTaskRunner) : AsyncFrostingTask<BuildContext>
{
    private readonly ResolveVersionsTaskRunner _resolveVersionsTaskRunner = resolveVersionsTaskRunner ?? throw new ArgumentNullException(nameof(resolveVersionsTaskRunner));

    public override Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return _resolveVersionsTaskRunner.RunAsync();
    }
}
