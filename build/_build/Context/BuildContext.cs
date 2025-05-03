using Build.Context.Configs;
using Build.Modules;
using Cake.Core;
using Cake.Frosting;

namespace Build.Context;

public sealed class BuildContext : FrostingContext
{
    public BuildContext(
        ICakeContext context,
        PathService pathService,
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

    public PathService Paths { get; }

    public RepositoryConfiguration Repo { get; }

    public DotNetBuildConfiguration DotNetConfiguration { get; }

    public VcpkgConfiguration Vcpkg { get; }

    public DumpbinConfiguration DumpbinConfiguration { get; }
}
