using Build.Application.Versioning;
using Build.Context;
using Cake.Frosting;

namespace Build.Tasks.Versioning;

/// <summary>
/// CI entrypoint for ADR-003 §3.1 build-host version resolution. Dispatches on
/// <c>--version-source</c> (<c>manifest</c> | <c>explicit</c> | <c>git-tag</c> | <c>meta-tag</c>)
/// and emits the resolved mapping to <c>artifacts/resolve-versions/versions.json</c> for
/// downstream CI jobs and local inspection.
/// <para>
/// Slice B1 wires the <c>manifest</c> source; other sources throw a clear "lands in later
/// slice" error.
/// </para>
/// </summary>
[TaskName("ResolveVersions")]
[TaskDescription("Resolves per-family versions from manifest/git-tag/explicit source and emits artifacts/resolve-versions/versions.json")]
public sealed class ResolveVersionsTask(ResolveVersionsTaskRunner resolveVersionsTaskRunner) : AsyncFrostingTask<BuildContext>
{
    private readonly ResolveVersionsTaskRunner _resolveVersionsTaskRunner = resolveVersionsTaskRunner ?? throw new ArgumentNullException(nameof(resolveVersionsTaskRunner));

    public override Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return _resolveVersionsTaskRunner.RunAsync();
    }
}
