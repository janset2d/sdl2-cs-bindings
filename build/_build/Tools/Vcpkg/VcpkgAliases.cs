using Build.Tools.Vcpkg.Settings;
using Cake.Core;
using Cake.Core.Annotations;
using Cake.Core.Diagnostics;

namespace Build.Tools.Vcpkg;

/// <summary>
/// Contains Cake aliases for running the 'vcpkg install' command.
/// </summary>
[CakeAliasCategory("Vcpkg")]
public static class VcpkgAliases
{
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
    /// Gets information about a single specified vcpkg package.
    /// Requires the --x-json flag, which is enabled by default in settings.
    /// </summary>
    /// <param name="context">The Cake context.</param>
    /// <param name="package">The package to get information for (e.g., "fmt", "zlib:x64-windows").</param>
    /// <param name="settings">The settings for the command.</param>
    /// <example>
    /// <code>
    /// string? infoJson = VcpkgPackageInfo("fmt", new VcpkgPackageInfoSettings { Triplet = "x64-windows" });
    /// </code>
    /// </example>
    [CakeMethodAlias]
    public static string? VcpkgPackageInfo(this ICakeContext context, string package, VcpkgPackageInfoSettings settings)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(settings);

        if (string.IsNullOrWhiteSpace(package))
        {
            throw new ArgumentNullException(nameof(package));
        }

        if (!settings.JsonOutput)
        {
            context.Log.Warning("Vcpkg 'x-package-info' typically requires JSON output (--x-json). Proceeding without it may fail.");
        }

        var tool = new VcpkgPackageInfoTool(context);
        return tool.GetPackageInfo(settings, package);
    }

    private static void RunVcpkgInstallInternal(ICakeContext context, IReadOnlyList<string>? packages, VcpkgInstallSettings settings)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(settings);

        var tool = new VcpkgInstallTool(context);
        tool.Install(settings, packages);
    }
}
