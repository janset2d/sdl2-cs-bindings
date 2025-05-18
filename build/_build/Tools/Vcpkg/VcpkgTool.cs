using Build.Tools.Vcpkg.Settings;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.IO;
using Cake.Core.Tooling;

namespace Build.Tools.Vcpkg;

/// <summary>
/// Base class for running Vcpkg commands. Handles tool resolution and common argument building.
/// </summary>
/// <typeparam name="TSettings">The type of settings used by the derived tool.</typeparam>
public abstract class VcpkgTool<TSettings> : Tool<TSettings> where TSettings : VcpkgSettings
{
    private readonly ICakeContext _cakeContext;
    private readonly IFileSystem _fileSystem;
    private readonly ICakeEnvironment _environment;

    protected VcpkgTool(ICakeContext cakeContext)
        : base(cakeContext.FileSystem, cakeContext.Environment, cakeContext.ProcessRunner, cakeContext.Tools)
    {
        _cakeContext = cakeContext;
        _fileSystem = _cakeContext.FileSystem;
        _environment = _cakeContext.Environment;
    }

    /// <summary>
    /// Gets the name of the tool.
    /// </summary>
    /// <returns>The name of the tool ("vcpkg").</returns>
    protected sealed override string GetToolName() => "vcpkg";

    /// <summary>
    /// Gets the possible names of the tool executable.
    /// </summary>
    /// <returns>The tool executable names ("vcpkg.exe", "vcpkg").</returns>
    protected sealed override IEnumerable<string> GetToolExecutableNames()
    {
        if (_environment.Platform.Family == PlatformFamily.Windows)
        {
            return ["vcpkg.exe", "vcpkg"];
        }

        return ["vcpkg", "vcpkg.exe"];
    }

    /// <summary>
    /// Gets alternative paths to the tool executable based on the VcpkgRoot setting.
    /// </summary>
    /// <param name="settings">The settings.</param>
    /// <returns>Enumeration of alternative paths.</returns>
    protected sealed override IEnumerable<FilePath> GetAlternativeToolPaths(TSettings settings)
    {
        var vcpkgRoot = settings.VcpkgRoot;
        if (!_cakeContext.DirectoryExists(vcpkgRoot))
        {
            throw new CakeException(
                $"Vcpkg root directory could not be determined or does not exist. Checked path: '{vcpkgRoot.FullPath}'. Use --vcpkg-dir argument or ensure vcpkg submodule exists.");
        }

        var exePath = vcpkgRoot.CombineWithFilePath(_environment.Platform.Family == PlatformFamily.Windows ? "vcpkg.exe" : "vcpkg");

        return _fileSystem.Exist(exePath) ? [exePath] : base.GetAlternativeToolPaths(settings);
    }

    /// <summary>
    /// Builds the common arguments based on the VcpkgSettings.
    /// </summary>
    /// <param name="settings">The Vcpkg settings.</param>
    /// <param name="builder">The argument builder to append arguments to.</param>
    protected void BuildCommonArguments(TSettings settings, ProcessArgumentBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(builder);

        if (!string.IsNullOrWhiteSpace(settings.Triplet))
        {
            builder.AppendSwitch("--triplet", settings.Triplet);
        }

        if (!string.IsNullOrWhiteSpace(settings.HostTriplet))
        {
            builder.AppendSwitch("--host-triplet", settings.HostTriplet);
        }

        if (settings.DownloadsRoot != null)
        {
            builder.AppendSwitch("--downloads-root", settings.DownloadsRoot.MakeAbsolute(_environment).FullPath);
        }

        if (settings.ClassicMode == true)
        {
            builder.Append("--classic");
        }

        foreach (var overlayPort in settings.OverlayPorts)
        {
            builder.AppendSwitch("--overlay-ports", overlayPort.MakeAbsolute(_environment).FullPath);
        }

        foreach (var overlayTriplet in settings.OverlayTriplets)
        {
            builder.AppendSwitch("--overlay-triplets", overlayTriplet.MakeAbsolute(_environment).FullPath);
        }

        foreach (var binarySource in settings.BinarySources)
        {
            // Might need escaping or quoting depending on the config format
            builder.AppendSwitch("--binarysource", binarySource);
        }

        if (settings.FeatureFlags.Count > 0)
        {
            builder.AppendSwitch("--feature-flags", string.Join(',', settings.FeatureFlags));
        }

        if (settings.BuildTreesRoot != null)
        {
            builder.AppendSwitch("--x-buildtrees-root", settings.BuildTreesRoot.MakeAbsolute(_environment).FullPath);
        }

        if (settings.InstallRoot != null)
        {
            builder.AppendSwitch("--x-install-root", settings.InstallRoot.MakeAbsolute(_environment).FullPath);
        }

        if (settings.ManifestRoot != null)
        {
            builder.AppendSwitch("--x-manifest-root", settings.ManifestRoot.MakeAbsolute(_environment).FullPath);
        }

        if (settings.PackagesRoot != null)
        {
            builder.AppendSwitch("--x-packages-root", settings.PackagesRoot.MakeAbsolute(_environment).FullPath);
        }

        if (!string.IsNullOrWhiteSpace(settings.AssetSources))
        {
            builder.AppendSwitch("--x-asset-sources", settings.AssetSources);
        }
    }
}
