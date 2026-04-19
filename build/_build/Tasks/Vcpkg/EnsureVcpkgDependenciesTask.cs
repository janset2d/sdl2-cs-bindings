using Build.Application.Vcpkg;
using Build.Context;
using Build.Tasks.Common;
using Cake.Frosting;

namespace Build.Tasks.Vcpkg;

[TaskName("EnsureVcpkgDependencies")]
[TaskDescription("Bootstraps vcpkg if needed and installs manifest dependencies for current runtime triplet")]
[IsDependentOn(typeof(InfoTask))]
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
