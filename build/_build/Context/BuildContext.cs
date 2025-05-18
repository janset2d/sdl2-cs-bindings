using Build.Context.Configs;
using Build.Modules.Contracts;
using Cake.Core;
using Cake.Frosting;

namespace Build.Context;

public sealed class BuildContext : FrostingContext
{
    public BuildContext(
        ICakeContext context,
        IPathService pathService,
        RepositoryConfiguration repoConfiguration,
        DotNetBuildConfiguration dotNetBuildConfiguration,
        VcpkgConfiguration vcpkgConfiguration,
        DumpbinConfiguration dumpbinConfiguration)
        : base(context)
    {
        Paths = pathService;
        Repo = repoConfiguration;
        DotNetConfiguration = dotNetBuildConfiguration;
        Vcpkg = vcpkgConfiguration;
        DumpbinConfiguration = dumpbinConfiguration;
    }

    public IPathService Paths { get; }

    public RepositoryConfiguration Repo { get; }

    public DotNetBuildConfiguration DotNetConfiguration { get; }

    public VcpkgConfiguration Vcpkg { get; }

    public DumpbinConfiguration DumpbinConfiguration { get; }
}
