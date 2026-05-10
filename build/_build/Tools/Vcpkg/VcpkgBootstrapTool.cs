using Build.Tools.Vcpkg.Settings;
using Cake.Common;
using Cake.Core;
using Cake.Core.IO;

namespace Build.Tools.Vcpkg;

public sealed class VcpkgBootstrapTool(ICakeContext cakeContext)
{
    private readonly ICakeContext _cakeContext = cakeContext ?? throw new ArgumentNullException(nameof(cakeContext));

    public void Bootstrap(VcpkgBootstrapSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (_cakeContext.Environment.Platform.Family == PlatformFamily.Windows)
        {
            RunBootstrapCommand(
                fileName: "cmd",
                arguments: new ProcessArgumentBuilder().Append("/c").AppendQuoted(settings.WindowsScript.FullPath),
                workingDirectory: settings.VcpkgRoot,
                description: "vcpkg bootstrap (Windows)");
            return;
        }

        RunBootstrapCommand(
            fileName: "bash",
            arguments: new ProcessArgumentBuilder().AppendQuoted(settings.UnixScript.FullPath),
            workingDirectory: settings.VcpkgRoot,
            description: "vcpkg bootstrap (Unix)");
    }

    private void RunBootstrapCommand(
        string fileName,
        ProcessArgumentBuilder arguments,
        DirectoryPath workingDirectory,
        string description)
    {
        var process = _cakeContext.StartAndReturnProcess(
            fileName,
            new ProcessSettings
            {
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                Silent = true,
            });

        process.WaitForExit();
        var exitCode = process.GetExitCode();

        if (exitCode == 0)
        {
            return;
        }

        var output = string.Join(
            Environment.NewLine,
            (process.GetStandardOutput() ?? []).Concat(process.GetStandardError() ?? []));

        throw new CakeException(
            $"{description} failed with exit code {exitCode}. Command: {fileName} {arguments.RenderSafe()}.{Environment.NewLine}{output}");
    }
}
