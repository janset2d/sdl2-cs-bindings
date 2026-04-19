using Build.Context.Configs;

namespace Build.Application.Packaging;

public interface IPackageTaskRunner
{
    Task RunAsync(CancellationToken cancellationToken = default);

    Task RunAsync(PackageBuildConfiguration packageBuildConfiguration, CancellationToken cancellationToken = default);
}
