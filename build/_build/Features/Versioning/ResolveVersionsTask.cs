using Build.Host;
using Cake.Frosting;

namespace Build.Features.Versioning;

/// <summary>
/// CI entrypoint for build-host version resolution.
/// Dispatches on <c>--version-source</c>, resolves a family/version mapping inside the
/// build host, and writes <c>artifacts/resolve-versions/versions.json</c> for downstream
/// jobs and local inspection.
/// </summary>
[TaskName("ResolveVersions")]
[TaskDescription("Resolves per-family versions and emits artifacts/resolve-versions/versions.json")]
public sealed class ResolveVersionsTask(ResolveVersionsPipeline resolveVersionsPipeline) : AsyncFrostingTask<BuildContext>
{
    private readonly ResolveVersionsPipeline _resolveVersionsPipeline = resolveVersionsPipeline ?? throw new ArgumentNullException(nameof(resolveVersionsPipeline));

    public override Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return _resolveVersionsPipeline.RunAsync();
    }
}
