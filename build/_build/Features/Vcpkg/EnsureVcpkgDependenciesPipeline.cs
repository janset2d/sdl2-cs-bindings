using Build.Host;
using Build.Integrations.Vcpkg;
using Build.Shared.Runtime;
using Build.Tools.Vcpkg;
using Build.Tools.Vcpkg.Settings;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;

namespace Build.Features.Vcpkg;

public sealed class EnsureVcpkgDependenciesPipeline(
    VcpkgBootstrapTool vcpkgBootstrapTool,
    IRuntimeProfile runtimeProfile,
    ICakeLog log)
{
    private readonly VcpkgBootstrapTool _vcpkgBootstrapTool = vcpkgBootstrapTool ?? throw new ArgumentNullException(nameof(vcpkgBootstrapTool));
    private readonly IRuntimeProfile _runtimeProfile = runtimeProfile ?? throw new ArgumentNullException(nameof(runtimeProfile));
    private readonly ICakeLog _log = log ?? throw new ArgumentNullException(nameof(log));

    // P4-deferred: this Pipeline still receives BuildContext because its own RunAsync(TRequest)
    // cut-over requires a VcpkgRequest DTO and the VcpkgTask signature change. Tracked in
    // phase-x §8.2 (remaining P4-B debt — EnsureVcpkgDependenciesPipeline).
    public void Run(BuildContext context)
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
