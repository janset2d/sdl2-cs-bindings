using System.Diagnostics.CodeAnalysis;
using Build.Tools.Vcpkg.Settings;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Spectre.Console;

namespace Build.Tools.Vcpkg;

/// <summary>
/// Tool runner for the 'vcpkg x-package-info' command.
/// </summary>
public sealed class VcpkgPackageInfoTool(ICakeContext cakeContext) : VcpkgTool<VcpkgPackageInfoSettings>(cakeContext)
{
    private readonly ICakeLog _log = cakeContext.Log;

    /// <summary>
    /// Executes the 'vcpkg x-package-info' command and returns the output.
    /// </summary>
    /// <param name="settings">The settings.</param>
    /// <param name="package">Package name to get info for.</param>
    public string? GetPackageInfo(VcpkgPackageInfoSettings settings, string package)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (string.IsNullOrWhiteSpace(package))
        {
            throw new ArgumentNullException(nameof(package));
        }

        var builder = BuildArguments(settings, package);

        _log.Verbose("Running vcpkg x-package-info with arguments: {0}", builder.RenderSafe());

        List<string> packageInfoOutputLines = [];
        List<string> packageInfoOutputLinesError = [];
        var processSettings = new ProcessSettings { RedirectStandardOutput = true, RedirectStandardError = true, };
        try
        {
            Run(settings, builder, processSettings, process =>
            {
                packageInfoOutputLinesError = [.. process.GetStandardError()];
                packageInfoOutputLines = [.. process.GetStandardOutput()];
            });
        }
        catch (CakeException) when (packageInfoOutputLinesError.Count == 0 && packageInfoOutputLines.Count > 0)
        {
            AnsiConsole.MarkupLine("[yellow]Warning:[/] Get the output from the command with exit code 1");
        }

        return packageInfoOutputLines.Count > 0 ? string.Join(Environment.NewLine, packageInfoOutputLines) : null;
    }

    /// <summary>
    /// Builds the argument builder for the 'vcpkg x-package-info' command.
    /// </summary>
    /// <param name="settings">The settings.</param>
    /// <param name="package">package name.</param>
    /// <returns>A ProcessArgumentBuilder.</returns>
    private ProcessArgumentBuilder BuildArguments(VcpkgPackageInfoSettings settings, string package)
    {
        var builder = new ProcessArgumentBuilder();
        builder.Append("x-package-info");
        builder.AppendQuoted(package);

        BuildCommonArguments(settings, builder);

        if (settings.Installed)
        {
            builder.Append("--x-installed");
        }


        if (settings.Transitive)
        {
            builder.Append("--x-transitive");
        }


        if (settings.JsonOutput)
        {
            builder.Append("--x-json"); // Add this (should be true by default)
        }


        return builder;
    }
}
