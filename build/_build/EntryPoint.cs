using System.CommandLine;
using System.Diagnostics;
using Build.Data;
using Build.Host;
using Build.Host.Cli;
using Build.Host.Cli.Options;
using Build.Targets.ConsolidateHarvest;
using Build.Targets.Harvest;
using Build.Targets.InspectHarvestedDependencies;
using Build.Targets.NativeSmoke;
using Build.Targets.OtoolAnalyze;
using Build.Targets.Package;
using Build.Targets.PackageConsumerSmoke;
using Build.Targets.PreFlightCheck;
using Build.Targets.PublishStaging;
using Build.Validation;
using Cake.Core.IO;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace Build;

internal static class EntryPoint
{
    internal static async Task<int> RunCakeHostAsync(
        ParseResult parseResult,
        Func<string[], Action<IServiceCollection>, int> runCake,
        CancellationToken cancellationToken)
    {
        var parsedArgs = CakeHostCommandLine.BindParsedArguments(parseResult);
        var repoRootPath = await DetermineRepoRootAsync(parsedArgs.RepoRoot);
        var effectiveArgs = GetEffectiveCakeArguments(repoRootPath, parseResult);

        // Keep Cake's synchronous run off the invocation thread so termination handlers can attach.
        return await Task.Run(() => runCake(effectiveArgs, services => ConfigureHostServices(services, parsedArgs, repoRootPath, cancellationToken)), cancellationToken);
    }

    internal static void ConfigureHostServices(
        IServiceCollection services,
        ParsedArguments parsedArgs,
        DirectoryPath repoRootPath,
        CancellationToken cancellationToken)
    {
        ConfigureBuildServices(services, parsedArgs, repoRootPath);
        // The generic overload cannot register a value type.
        services.AddSingleton(typeof(CancellationToken), cancellationToken);
    }

    internal static void ConfigureBuildServices(IServiceCollection services, ParsedArguments parsedArgs, DirectoryPath repoRootPath)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(parsedArgs);

        services.AddSingleton(AnsiConsole.Console);
        services
            .AddHostBuildingBlocks(parsedArgs, repoRootPath)
            .AddData()
            .AddValidators()
            .AddInspectHarvestedDependenciesTarget()
            .AddOtoolAnalyzeTarget()
            .AddPreFlightCheck()
            .AddHarvest()
            .AddNativeSmoke()
            .AddConsolidateHarvest()
            .AddPackage()
            .AddPackageConsumerSmoke()
            .AddPublishStaging();
    }

    internal static async Task<DirectoryPath> DetermineRepoRootAsync(DirectoryInfo? repoRootArg)
    {
        if (repoRootArg?.Exists == true)
        {
            AnsiConsole.MarkupLine($"[green]Using Repository Root from --repo-root argument:[/] {repoRootArg.FullName}");
            return new DirectoryPath(repoRootArg.FullName);
        }

        var gitOutput = string.Empty;
        var exitCode = -1;

        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "rev-parse --show-toplevel",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Environment.CurrentDirectory,
            };
            process.Start();
            gitOutput = (await process.StandardOutput.ReadToEndAsync()).Trim();
            await process.WaitForExitAsync();
            exitCode = process.ExitCode;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[yellow]Warning:[/] Failed to execute 'git rev-parse --show-toplevel'. Error: {ex.Message}");
        }

        if (exitCode == 0 && !string.IsNullOrWhiteSpace(gitOutput) && Directory.Exists(gitOutput))
        {
            AnsiConsole.MarkupLine($"[green]Determined Repository Root via git:[/] {gitOutput}");
            return new DirectoryPath(gitOutput);
        }

        var fallbackPath = new DirectoryPath(AppContext.BaseDirectory).GetParent()?.GetParent()?.Collapse();

        if (fallbackPath != null)
        {
            AnsiConsole.MarkupLine($"[yellow]Warning:[/] Could not determine repo root via git. Assuming relative path: {fallbackPath.FullPath}");
            return fallbackPath;
        }

        var absoluteFallback = new DirectoryPath(Environment.CurrentDirectory);
        AnsiConsole.MarkupLine($"[red]ERROR:[/] Could not determine repo root via git or relative path. Using CWD: {absoluteFallback.FullPath}");
        return absoluteFallback;
    }

    internal static string[] GetEffectiveCakeArguments(DirectoryPath repoRoot, ParseResult parseResult)
    {
        var originalArgs = parseResult.Tokens.Select(token => token.Value).ToArray();
        var explicitVerbositySet = parseResult.GetResult(CakeOptions.VerbosityOption) is not null;
        var explicitWorkingPathSet = parseResult.GetResult(CakeOptions.WorkingPathOption) is not null;
        var isGitHubDebugRun = string.Equals(Environment.GetEnvironmentVariable("ACTIONS_RUNNER_DEBUG"), "true", StringComparison.OrdinalIgnoreCase);
        var shouldInjectVerbosity = isGitHubDebugRun && !explicitVerbositySet;
        var shouldInjectWorkingPath = !explicitWorkingPathSet;

        if (!shouldInjectVerbosity && !shouldInjectWorkingPath)
        {
            return originalArgs;
        }

        var indicesToSkip = new HashSet<int>();

        for (var i = 0; i < originalArgs.Length; i++)
        {
            var arg = originalArgs[i];

            if ((!shouldInjectVerbosity || !IsVerbosityArg(arg)) && (!shouldInjectWorkingPath || !IsWorkingPathArg(arg)))
            {
                continue;
            }

            indicesToSkip.Add(i);

            if (i + 1 < originalArgs.Length && !originalArgs[i + 1].StartsWith('-'))
            {
                indicesToSkip.Add(i + 1);
            }
        }

        var estimatedCapacity = originalArgs.Length - indicesToSkip.Count +
                                (shouldInjectVerbosity ? 2 : 0) +
                                (shouldInjectWorkingPath ? 2 : 0);
        var result = new List<string>(estimatedCapacity);
        result.AddRange(originalArgs.Where((t, i) => !indicesToSkip.Contains(i)));

        if (shouldInjectVerbosity)
        {
            result.Add("--verbosity");
            result.Add("diagnostic");
            AnsiConsole.MarkupLine("[yellow]ACTIONS_RUNNER_DEBUG=true detected (and no explicit verbosity set). Forcing Cake verbosity to Diagnostic.[/]");
        }

        if (!shouldInjectWorkingPath)
        {
            return [.. result];
        }

        result.Add("--working");
        result.Add(repoRoot.FullPath);
        AnsiConsole.MarkupLine($"[yellow]No explicit working path set. Forcing Cake working path to {repoRoot.FullPath}.[/]");
        return [.. result];
    }

    internal static bool IsVerbosityArg(string arg) =>
        string.Equals(arg, "--verbosity", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(arg, "-v", StringComparison.OrdinalIgnoreCase);

    internal static bool IsWorkingPathArg(string arg) =>
        string.Equals(arg, "--working", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(arg, "-w", StringComparison.OrdinalIgnoreCase);
}
