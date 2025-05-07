using Build.Tools.Vcpkg.Settings;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;

namespace Build.Tools.Vcpkg;

/// <summary>
/// Tool runner for the 'vcpkg install' command.
/// </summary>
public sealed class VcpkgInstallTool(ICakeContext cakeContext)
    : VcpkgTool<VcpkgInstallSettings>(cakeContext)
{
    private readonly ICakeLog _log = cakeContext.Log;

    /// <summary>
    /// Executes the 'vcpkg install' command.
    /// </summary>
    /// <param name="settings">The settings.</param>
    /// <param name="packages">Optional list of packages for Classic mode.</param>
    public void Install(VcpkgInstallSettings settings, IReadOnlyList<string>? packages = null)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (settings.ClassicMode == true && packages?.Any() != true)
        {
            throw new ArgumentException("At least one package must be specified when running 'vcpkg install' in Classic Mode.", nameof(packages));
        }
        // Could add check: if Manifest Mode (settings.ClassicMode == false?) and packages *are* provided, maybe warn or error?

        var builder = BuildArguments(settings, packages);

        _log.Verbose("Running vcpkg install with arguments: {0}", builder.RenderSafe());

        Run(settings, builder);
    }

    /// <summary>
    /// Builds the argument builder for the 'vcpkg install' command.
    /// </summary>
    /// <param name="settings">The settings.</param>
    /// <param name="packages">Optional list of packages for Classic mode.</param>
    /// <returns>A ProcessArgumentBuilder.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051:Method is too long", Justification = "<Pending>")]
    private ProcessArgumentBuilder BuildArguments(VcpkgInstallSettings settings, IEnumerable<string>? packages)
    {
        var builder = new ProcessArgumentBuilder();
        builder.Append("install");

        BuildCommonArguments(settings, builder);

        if (packages != null)
        {
            foreach (var pkg in packages)
            {
                if (!string.IsNullOrWhiteSpace(pkg))
                {
                    builder.Append(pkg);
                }
            }
        }

        // Add install-specific arguments
        if (settings.AllowUnsupported)
        {
            builder.Append("--allow-unsupported");
        }

        if (settings.CleanAfterBuild)
        {
            builder.Append("--clean-after-build");
        }

        if (settings.CleanBuildTreesAfterBuild)
        {
            builder.Append("--clean-buildtrees-after-build");
        }

        if (settings.CleanDownloadsAfterBuild)
        {
            builder.Append("--clean-downloads-after-build");
        }

        if (settings.CleanPackagesAfterBuild)
        {
            builder.Append("--clean-packages-after-build");
        }

        if (settings.DryRun)
        {
            builder.Append("--dry-run");
        }

        if (settings.Editable)
        {
            builder.Append("--editable"); // Classic mode only
        }

        if (settings.EnforcePortChecks)
        {
            builder.Append("--enforce-port-checks");
        }

        foreach (var feature in settings.Features) // Manifest mode only
        {
            builder.AppendSwitch("--x-feature", feature);
        }

        if (settings.Head)
        {
            builder.Append("--head"); // Classic mode only
        }

        if (settings.KeepGoing)
        {
            builder.Append("--keep-going");
        }

        if (settings.NoDefaultFeatures)
        {
            builder.Append("--x-no-default-features"); // Manifest mode only
        }

        if (settings.NoDownloads)
        {
            builder.Append("--no-downloads");
        }

        if (settings.OnlyDownloads)
        {
            builder.Append("--only-downloads");
        }

        if (settings.OnlyBinaryCaching)
        {
            builder.Append("--only-binarycaching");
        }

        if (settings.Recurse)
        {
            builder.Append("--recurse"); // Classic mode only
        }

        if (settings.WriteNuGetPackagesConfig)
        {
            builder.Append("--x-write-nuget-packages-config");
        }

        if (settings.NoPrintUsage)
        {
            builder.Append("--no-print-usage");
        }

        return builder;
    }
}
