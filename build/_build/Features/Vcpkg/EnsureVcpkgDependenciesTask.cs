using Build.Host;
using Cake.Frosting;

namespace Build.Features.Vcpkg;

[TaskName("EnsureVcpkgDependencies")]
[TaskDescription("Bootstraps vcpkg if needed and installs manifest dependencies for current runtime triplet")]
public sealed class EnsureVcpkgDependenciesTask(
    EnsureVcpkgDependenciesPipeline ensureVcpkgDependenciesPipeline) : FrostingTask<BuildContext>
{
    private readonly EnsureVcpkgDependenciesPipeline _ensureVcpkgDependenciesPipeline = ensureVcpkgDependenciesPipeline ?? throw new ArgumentNullException(nameof(ensureVcpkgDependenciesPipeline));

    public override void Run(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _ensureVcpkgDependenciesPipeline.Run(context);
    }
}
