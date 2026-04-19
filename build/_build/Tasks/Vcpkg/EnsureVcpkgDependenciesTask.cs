using Build.Context;
using Build.Domain.Runtime;
using Build.Infrastructure.Tools.Vcpkg;
using Build.Infrastructure.Tools.Vcpkg.Settings;
using Build.Tasks.Common;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Cake.Frosting;

namespace Build.Tasks.Vcpkg;

[TaskName("EnsureVcpkgDependencies")]
[TaskDescription("Bootstraps vcpkg if needed and installs manifest dependencies for current runtime triplet")]
[IsDependentOn(typeof(InfoTask))]
public sealed class EnsureVcpkgDependenciesTask(
    VcpkgBootstrapTool vcpkgBootstrapTool,
    IRuntimeProfile runtimeProfile,
    ICakeLog log) : FrostingTask<BuildContext>
{
    private readonly VcpkgBootstrapTool _vcpkgBootstrapTool = vcpkgBootstrapTool ?? throw new ArgumentNullException(nameof(vcpkgBootstrapTool));
    private readonly IRuntimeProfile _runtimeProfile = runtimeProfile ?? throw new ArgumentNullException(nameof(runtimeProfile));
    private readonly ICakeLog _log = log ?? throw new ArgumentNullException(nameof(log));

    public override void Run(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        EnsureVcpkgBootstrapped(context);

        _log.Information(
            "Installing vcpkg dependencies for triplet '{0}' with overlays '{1}' and '{2}'.",
            _runtimeProfile.Triplet,
            context.Paths.VcpkgOverlayTripletsDir.FullPath,
            context.Paths.VcpkgOverlayPortsDir.FullPath);

        var installSettings = new VcpkgInstallSettings(context.Paths.VcpkgRoot)
        {
            Triplet = _runtimeProfile.Triplet,
            ManifestRoot = context.Paths.RepoRoot,
            OverlayTriplets = new List<DirectoryPath> { context.Paths.VcpkgOverlayTripletsDir },
            OverlayPorts = new List<DirectoryPath> { context.Paths.VcpkgOverlayPortsDir },
        };

        var installTool = new VcpkgInstallTool(context);
        installTool.Install(installSettings);
    }

    private void EnsureVcpkgBootstrapped(BuildContext context)
    {
        var hasWindowsExecutable = context.FileExists(context.Paths.VcpkgWindowsExecutableFile);
        var hasUnixExecutable = context.FileExists(context.Paths.VcpkgUnixExecutableFile);

        if (hasWindowsExecutable || hasUnixExecutable)
        {
            _log.Verbose("vcpkg executable already present under {0}.", context.Paths.VcpkgRoot.FullPath);
            return;
        }

        if (!context.FileExists(context.Paths.VcpkgBootstrapBatchScript))
        {
            throw new CakeException($"Cannot bootstrap vcpkg: '{context.Paths.VcpkgBootstrapBatchScript.FullPath}' does not exist.");
        }

        if (!context.FileExists(context.Paths.VcpkgBootstrapShellScript))
        {
            throw new CakeException($"Cannot bootstrap vcpkg: '{context.Paths.VcpkgBootstrapShellScript.FullPath}' does not exist.");
        }

        _vcpkgBootstrapTool.Bootstrap(
            context.Paths.VcpkgRoot,
            context.Paths.VcpkgBootstrapBatchScript,
            context.Paths.VcpkgBootstrapShellScript);
    }
}
