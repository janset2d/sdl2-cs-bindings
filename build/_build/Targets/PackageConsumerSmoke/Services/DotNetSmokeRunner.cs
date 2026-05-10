using Cake.Common;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;

namespace Build.Targets.PackageConsumerSmoke.Services;

/// <summary>
/// Process spawn + stdout/stderr capture + build-server lifecycle for the
/// PackageConsumerSmoke target's <c>dotnet build</c> / <c>dotnet test</c> invocations.
/// Bundles three concerns:
/// <list type="bullet">
///   <item><description>Build-server flag injection on the <c>dotnet build</c> compile-sanity path (<c>--disable-build-servers</c>, <c>-p:UseSharedCompilation=false</c>, <c>-nodeReuse:false</c>). The smoke <c>dotnet test</c> path runs through Microsoft.Testing.Platform — MTP rejects unknown SDK flags, so the smoke args carry only <c>-p:UseSharedCompilation=false</c> baked in by the caller.</description></item>
///   <item><description>Best-effort <c>dotnet build-server shutdown</c> between TFMs. Keeps multi-target sequences stable on Windows where leftover servers intermittently break the next net4x run.</description></item>
///   <item><description>Combined output capture with caller-chosen verbosity. Failure throws <c>CakeException</c> with full stdout/stderr in the message so operators see the actual error.</description></item>
/// </list>
/// </summary>
public sealed class DotNetSmokeRunner(ICakeContext cakeContext, ICakeLog log)
{
    private readonly ICakeContext _cakeContext = cakeContext ?? throw new ArgumentNullException(nameof(cakeContext));
    private readonly ICakeLog _log = log ?? throw new ArgumentNullException(nameof(log));

    public void RunCompileSanity(
        string description,
        ProcessArgumentBuilder arguments,
        DirectoryPath workingDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(workingDirectory);

        AppendBuildServerSuppressionFlags(arguments);
        ExecuteDotNet(description, arguments, workingDirectory, echoStdout: true, environmentVariables: null);
    }

    public void RunSmokeForTfm(
        string description,
        ProcessArgumentBuilder arguments,
        DirectoryPath workingDirectory,
        IReadOnlyDictionary<string, string>? environmentVariables = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(workingDirectory);

        // Microsoft.Testing.Platform (TUnit) intercepts the dotnet test runner and rejects
        // unknown SDK options. --disable-build-servers / -nodeReuse:false belong on the
        // build path; the smoke args already carry -p:UseSharedCompilation=false baked in
        // by the task-side argument builder.
        ExecuteDotNet(description, arguments, workingDirectory, echoStdout: true, environmentVariables);
    }

    /// <summary>
    /// Best-effort <c>dotnet build-server shutdown</c>. Failures are logged at verbose
    /// level and do not abort the run — the build-server flags on individual invocations
    /// remain the primary defence.
    /// </summary>
    public void ShutdownBuildServers(DirectoryPath workingDirectory)
    {
        ArgumentNullException.ThrowIfNull(workingDirectory);

        var arguments = new ProcessArgumentBuilder()
            .Append("build-server")
            .Append("shutdown");

        var process = _cakeContext.StartAndReturnProcess(
            "dotnet",
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
        if (exitCode != 0)
        {
            _log.Verbose("dotnet build-server shutdown returned exit code {0} (best-effort; run continues).", exitCode);
        }
        else
        {
            _log.Verbose("dotnet build-server shutdown completed.");
        }
    }

    private static void AppendBuildServerSuppressionFlags(ProcessArgumentBuilder arguments)
    {
        arguments
            .Append("--disable-build-servers")
            .Append("-p:UseSharedCompilation=false")
            .Append("-nodeReuse:false");
    }

    private void ExecuteDotNet(
        string description,
        ProcessArgumentBuilder arguments,
        DirectoryPath workingDirectory,
        bool echoStdout,
        IReadOnlyDictionary<string, string>? environmentVariables)
    {
        _log.Information("Running dotnet {0}", description);
        _log.Verbose("  dotnet {0}", arguments.Render());

        var process = _cakeContext.StartAndReturnProcess(
            "dotnet",
            new ProcessSettings
            {
                Arguments = arguments,
                EnvironmentVariables = environmentVariables is null || environmentVariables.Count == 0
                    ? null
                    : new Dictionary<string, string>(environmentVariables, StringComparer.OrdinalIgnoreCase),
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                Silent = true,
            });

        process.WaitForExit();

        var standardOutput = process.GetStandardOutput()?.ToList() ?? [];
        var standardError = process.GetStandardError()?.ToList() ?? [];

        var stdoutLevel = echoStdout ? LogLevel.Information : LogLevel.Verbose;
        var stderrLevel = echoStdout ? LogLevel.Warning : LogLevel.Verbose;

        foreach (var line in standardOutput)
        {
            _log.Write(Verbosity.Normal, stdoutLevel, "  [stdout] {0}", line);
        }

        foreach (var line in standardError)
        {
            _log.Write(Verbosity.Normal, stderrLevel, "  [stderr] {0}", line);
        }

        var exitCode = process.GetExitCode();
        if (exitCode != 0)
        {
            var combinedOutput = string.Join(Environment.NewLine, standardOutput.Concat(standardError));
            throw new CakeException(
                $"dotnet {description} failed with exit code {exitCode}.{Environment.NewLine}{combinedOutput}");
        }
    }
}
