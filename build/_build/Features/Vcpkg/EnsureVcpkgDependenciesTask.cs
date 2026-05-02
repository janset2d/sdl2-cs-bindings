using Build.Host;
using Cake.Frosting;

namespace Build.Features.Vcpkg;

[TaskName("EnsureVcpkgDependencies")]
[TaskDescription("Bootstraps vcpkg if needed and installs manifest dependencies for current runtime triplet")]
public sealed class EnsureVcpkgDependenciesTask(
    EnsureVcpkgDependenciesTaskRunner ensureVcpkgDependenciesTaskRunner) : FrostingTask<BuildContext>
{
    private readonly EnsureVcpkgDependenciesTaskRunner _ensureVcpkgDependenciesTaskRunner = ensureVcpkgDependenciesTaskRunner ?? throw new ArgumentNullException(nameof(ensureVcpkgDependenciesTaskRunner));

    public override void Run(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _ensureVcpkgDependenciesTaskRunner.Run(context);
    }
}
