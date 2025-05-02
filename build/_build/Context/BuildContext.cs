using Build.Context.Settings;
using Build.Modules;
using Cake.Core;
using Cake.Frosting;

namespace Build.Context;

public sealed class BuildContext : FrostingContext
{
    public BuildContext(
        ICakeContext context,
        PathService pathService,
        RepositorySettings repoSettings,
        DotNetBuildSettings dotNetBuildSettings,
        VcpkgSettings vcpkgSettings)
        : base(context)
    {
        Paths = pathService;
        Repo = repoSettings;
        Settings = dotNetBuildSettings;
        Vcpkg = vcpkgSettings;
    }

    public PathService Paths { get; }

    public RepositorySettings Repo { get; }

    public DotNetBuildSettings Settings { get; }

    public VcpkgSettings Vcpkg { get; }
}
