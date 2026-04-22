using Cake.Core;
using Cake.Core.IO;
using Cake.Core.Tooling;

namespace Build.Infrastructure.Tools.NativeSmoke;

/// <summary>
/// Cake tool wrapper for executing the native-smoke runtime validation binary.
/// </summary>
public sealed class NativeSmokeRunnerTool(ICakeContext cakeContext)
    : Tool<NativeSmokeRunnerSettings>(cakeContext.FileSystem, cakeContext.Environment, cakeContext.ProcessRunner, cakeContext.Tools)
{
    protected override string GetToolName() => "native-smoke";

    protected override IEnumerable<string> GetToolExecutableNames() =>
    [
        "native-smoke.exe",
        "native-smoke",
    ];

    protected override IEnumerable<FilePath> GetAlternativeToolPaths(NativeSmokeRunnerSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return [settings.ExecutablePath];
    }

    public NativeSmokeRunnerResult RunSmoke(NativeSmokeRunnerSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var args = new ProcessArgumentBuilder();
        foreach (var argument in settings.Arguments)
        {
            args.Append(argument);
        }

        var processSettings = new ProcessSettings
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        List<string> stdout = [];
        List<string> stderr = [];
        var exitCode = 0;

        Run(settings, args, processSettings, process =>
        {
            stdout = [.. process.GetStandardOutput()];
            stderr = [.. process.GetStandardError()];
            exitCode = process.GetExitCode();
        });

        return new NativeSmokeRunnerResult(exitCode, stdout, stderr);
    }
}
