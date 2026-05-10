using Build.Results;
using Build.Tools.Vcpkg.Settings;
using Cake.Core;
using Cake.Core.Annotations;
using Cake.Core.Diagnostics;
using Cake.Core.IO;

namespace Build.Tools.Vcpkg;

/// <summary>
/// Contains Cake aliases for running the 'vcpkg install' command.
/// </summary>
[CakeAliasCategory("Vcpkg")]
public static class VcpkgAliases
{
    /// <summary>
    /// Bootstraps vcpkg by running the platform-specific bootstrap script.
    /// </summary>
    /// <param name="context">The Cake context.</param>
    /// <param name="settings">Bootstrap script paths and vcpkg root.</param>
    [CakeMethodAlias]
    public static void VcpkgBootstrap(this ICakeContext context, VcpkgBootstrapSettings settings)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(settings);

        var tool = new VcpkgBootstrapTool(context);
        tool.Bootstrap(settings);
    }

    /// <summary>
    /// Installs packages based on the vcpkg manifest file (vcpkg.json).
    /// Use this overload for Manifest mode.
    /// </summary>
    /// <param name="context">The Cake context.</param>
    /// <param name="settings">The settings for the install command.</param>
    /// <example>
    /// <code>
    /// VcpkgInstall(new VcpkgInstallSettings { Triplet = "x64-windows" });
    /// </code>
    /// </example>
    [CakeMethodAlias]
    public static void VcpkgInstall(this ICakeContext context, VcpkgInstallSettings settings)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(settings);

        RunVcpkgInstallInternal(context, packages: null, settings);
    }

    /// <summary>
    /// Installs the specified list of vcpkg packages.
    /// Use this overload for Classic mode.
    /// </summary>
    /// <param name="context">The Cake context.</param>
    /// <param name="packages">The packages to install (e.g., "fmt", "zlib:x64-windows", "imgui[docking]").</param>
    /// <param name="settings">The settings for the install command.</param>
    /// <example>
    /// <code>
    /// VcpkgInstall(new[] { "fmt", "zlib" }, new VcpkgInstallSettings { Triplet = "x64-windows" });
    /// </code>
    /// </example>
    [CakeMethodAlias]
    public static void VcpkgInstall(this ICakeContext context, IReadOnlyList<string> packages, VcpkgInstallSettings settings)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(settings);

        if (!packages.Any())
        {
            throw new ArgumentNullException(nameof(packages), "At least one package must be specified for vcpkg install in Classic mode.");
        }

        RunVcpkgInstallInternal(context, packages, settings);
    }

    /// <summary>
    /// Installs a single specified vcpkg package.
    /// Convenience overload for Classic mode.
    /// </summary>
    /// <param name="context">The Cake context.</param>
    /// <param name="package">The package to install (e.g., "fmt", "zlib:x64-windows", "imgui[docking]").</param>
    /// <param name="settings">The settings for the install command.</param>
    /// <example>
    /// <code>
    /// VcpkgInstall("fmt", new VcpkgInstallSettings { Triplet = "x64-windows" });
    /// </code>
    /// </example>
    [CakeMethodAlias]
    public static void VcpkgInstall(this ICakeContext context, string package, VcpkgInstallSettings settings)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(settings);

        if (string.IsNullOrWhiteSpace(package))
        {
            throw new ArgumentNullException(nameof(package));
        }

        RunVcpkgInstallInternal(context, [package], settings);
    }

    /// <summary>
    /// Gets raw JSON information about a single specified vcpkg package.
    /// Requires the --x-json flag, which is enabled by default in settings.
    /// </summary>
    /// <param name="context">The Cake context.</param>
    /// <param name="package">The package to get information for (e.g., "fmt", "zlib:x64-windows").</param>
    /// <param name="settings">The settings for the command.</param>
    /// <example>
    /// <code>
    /// string? infoJson = VcpkgPackageInfoJson("fmt", new VcpkgPackageInfoSettings { Triplet = "x64-windows" });
    /// </code>
    /// </example>
    [CakeMethodAlias]
    public static string? VcpkgPackageInfoJson(this ICakeContext context, string package, VcpkgPackageInfoSettings settings)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentException.ThrowIfNullOrWhiteSpace(package);

        if (!settings.JsonOutput)
        {
            context.Log.Warning("Vcpkg 'x-package-info' typically requires JSON output (--x-json). Proceeding without it may fail.");
        }

        var tool = new VcpkgPackageInfoTool(context);
        return tool.GetPackageInfoJson(settings, package);
    }

    /// <summary>
    /// Gets typed installed-package information from vcpkg x-package-info JSON output.
    /// </summary>
    /// <param name="context">The Cake context.</param>
    /// <param name="packageName">The package name without triplet (e.g., "sdl2-image").</param>
    /// <param name="triplet">The vcpkg triplet (e.g., "x64-windows-hybrid").</param>
    /// <param name="installedRoot">The root directory containing installed vcpkg files.</param>
    /// <param name="settings">The settings for the command.</param>
    [CakeMethodAlias]
    public static Result<VcpkgPackageInfo, VcpkgPackageInfoError> VcpkgPackageInfo(
        this ICakeContext context,
        string packageName,
        string triplet,
        DirectoryPath installedRoot,
        VcpkgPackageInfoSettings settings)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageName);
        ArgumentException.ThrowIfNullOrWhiteSpace(triplet);
        ArgumentNullException.ThrowIfNull(installedRoot);
        ArgumentNullException.ThrowIfNull(settings);

        var packageKey = VcpkgPackageKey.Create(packageName, triplet);
        var json = context.VcpkgPackageInfoJson(packageKey, WithInstalledJsonDefaults(settings));
        return VcpkgPackageInfoParser.ParseInstalledPackageInfo(json, packageName, triplet, installedRoot);
    }

    private static VcpkgPackageInfoSettings WithInstalledJsonDefaults(VcpkgPackageInfoSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new VcpkgPackageInfoSettings(settings.VcpkgRoot)
        {
            Triplet = settings.Triplet,
            HostTriplet = settings.HostTriplet,
            DownloadsRoot = settings.DownloadsRoot,
            ClassicMode = settings.ClassicMode,
            OverlayPorts = [.. settings.OverlayPorts],
            OverlayTriplets = [.. settings.OverlayTriplets],
            BinarySources = [.. settings.BinarySources],
            FeatureFlags = [.. settings.FeatureFlags],
            BuildTreesRoot = settings.BuildTreesRoot,
            InstallRoot = settings.InstallRoot,
            ManifestRoot = settings.ManifestRoot,
            PackagesRoot = settings.PackagesRoot,
            AssetSources = settings.AssetSources,
            Installed = true,
            Transitive = settings.Transitive,
            JsonOutput = true,
        };
    }

    private static void RunVcpkgInstallInternal(ICakeContext context, IReadOnlyList<string>? packages, VcpkgInstallSettings settings)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(settings);

        var tool = new VcpkgInstallTool(context);
        tool.Install(settings, packages);
    }
}
