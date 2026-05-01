using Build.Context.Configs;
using Build.Domain.Paths;
using Build.Domain.Runtime;
using Cake.Core;
using Cake.Frosting;

namespace Build.Context;

public sealed class BuildContext : FrostingContext
{
    public BuildContext(
        ICakeContext context,
        IPathService pathService,
        IRuntimeProfile runtimeProfile,
        RepositoryConfiguration repoConfiguration,
        DotNetBuildConfiguration dotNetBuildConfiguration,
        VcpkgConfiguration vcpkgConfiguration,
        DumpbinConfiguration dumpbinConfiguration)
        : base(context)
    {
        Paths = pathService;
        Runtime = runtimeProfile;
        Repo = repoConfiguration;
        DotNetConfiguration = dotNetBuildConfiguration;
        Vcpkg = vcpkgConfiguration;
        DumpbinConfiguration = dumpbinConfiguration;
    }

    public IPathService Paths { get; }

    public IRuntimeProfile Runtime { get; }

    public RepositoryConfiguration Repo { get; }

    public DotNetBuildConfiguration DotNetConfiguration { get; }

    public VcpkgConfiguration Vcpkg { get; }

    public DumpbinConfiguration DumpbinConfiguration { get; }
}
