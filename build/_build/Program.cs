# pragma warning disable CA1031, MA0045, MA0051, CA1502, CA1505

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.NamingConventionBinder;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Build;
using Build.Targets.ConsolidateHarvest;
using Build.Targets.Harvest;
using Build.Targets.InspectHarvestedDependencies;
using Build.Targets.NativeSmoke;
using Build.Targets.OtoolAnalyze;
using Build.Targets.Package;
using Build.Targets.PackageConsumerSmoke;
using Build.Targets.PreFlightCheck;
using Build.Targets.PublishStaging;
using Build.Host;
using Build.Host.Cli.Options;
using Build.Data;
using Build.Validation;
using Cake.Core.IO;
using Cake.Frosting;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

var root = new RootCommand("Cake build for janset2d/sdl2-cs-bindings");

root.AddOption(CakeOptions.DescriptionOption);
root.AddOption(CakeOptions.DryRunOption);
root.AddOption(CakeOptions.ExclusiveOption);
root.AddOption(CakeOptions.HelpOption);
root.AddOption(CakeOptions.InfoOption);
root.AddOption(CakeOptions.TargetOption);
root.AddOption(CakeOptions.TreeOption);
root.AddOption(CakeOptions.VerbosityOption);
root.AddOption(CakeOptions.VersionOption);
root.AddOption(CakeOptions.WorkingPathOption);

root.AddOption(RepositoryOptions.RepoRootOption);

root.AddOption(DotNetOptions.ConfigOption);

root.AddOption(VcpkgOptions.VcpkgDirOption);
root.AddOption(VcpkgOptions.VcpkgInstalledDirOption);
root.AddOption(VcpkgOptions.LibraryOption);
root.AddOption(VcpkgOptions.RidOption);

root.AddOption(ResolveVersionsFromManifestOptions.VersionSuffixOption);
root.AddOption(ResolveVersionsFromManifestOptions.VersionScopeOption);

root.AddOption(ResolveVersionsFromExplicitOptions.ExplicitVersionOption);
root.AddOption(ResolveVersionsFromExplicitOptions.ExplicitVersionsOption);

root.AddOption(StageVersionsOptions.VersionsFileOption);

root.AddOption(DumpbinOptions.DllOption);

root.Handler = CommandHandler.Create<InvocationContext, ParsedArguments>(RunCakeHostAsync);
return await root.InvokeAsync(args);

static async Task<int> RunCakeHostAsync(InvocationContext context, ParsedArguments parsedArgs)
{
    var repoRootPath = await DetermineRepoRootAsync(parsedArgs.RepoRoot);
    var initialCakeArgs = context.ParseResult.Tokens.Select(t => t.Value).ToArray();
    var effectiveCakeArgs = GetEffectiveCakeArguments(initialCakeArgs, repoRootPath, context);

    using var cts = new CancellationTokenSource();

    // Console.CancelKeyPress fires for Ctrl+C on all platforms; setting e.Cancel=true
    // prevents immediate process termination so collaborators get a chance to clean up.
    // A second Ctrl+C falls through to the default OS handler and force-quits.
    void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        if (cts.IsCancellationRequested)
        {
            return;
        }
        e.Cancel = true;
        cts.Cancel();
    }
    Console.CancelKeyPress += OnCancelKeyPress;

    // PosixSignalRegistration handles SIGTERM on Unix (kill <pid>, container stop, etc.).
    // No-op on Windows. SIGINT is already covered by Console.CancelKeyPress.
    using var sigTermRegistration = PosixSignalRegistration.Create(PosixSignal.SIGTERM, _ => cts.Cancel());

    try
    {
        return new CakeHost()
            .UseContext<BuildContext>()
            .ConfigureServices(services =>
            {
                ConfigureBuildServices(services, parsedArgs, repoRootPath);
                // CancellationToken is a struct; the generic AddSingleton<T> overload has a
                // `where T : class` constraint, so register via the non-generic Type + instance
                // overload. ActivatorUtilities unboxes when resolving BuildContext's ctor param.
                services.AddSingleton(typeof(CancellationToken), cts.Token);
            })
            .Run(effectiveCakeArgs);
    }
    finally
    {
        Console.CancelKeyPress -= OnCancelKeyPress;
    }
}

static void ConfigureBuildServices(IServiceCollection services, ParsedArguments parsedArgs, DirectoryPath repoRootPath)
{
    ArgumentNullException.ThrowIfNull(services);
    ArgumentNullException.ThrowIfNull(parsedArgs);

    // Stage tasks (PreFlight, Package, ConsumerSmoke, PublishStaging) load the resolved
    // family→version mapping from `context.VersionsFilePath` via IVersionFileRepository.
    // Each task validates the path + non-empty mapping at task entry. ResolveVersions
    // tasks emit the file via the same repository's SaveAsync, so DI only needs the
    // repository plus the path surfaced on BuildContext.
    services.AddSingleton<IAnsiConsole>(AnsiConsole.Console);

    // Composition root: per-target AddXTarget() calls + cross-cutting groupings
    // (AddHostBuildingBlocks, AddData, AddValidators).
    // AddHostBuildingBlocks takes parsedArgs + repoRoot because IPathService composes its
    // layout from CLI overrides before any DI resolution.
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

    // PublishPublicTask discovered by Cake via [TaskName]; no AddPublishPublic() since the
    // stub registers nothing. Real implementation lands as PD-7 in Phase 2b.
}

static async Task<DirectoryPath> DetermineRepoRootAsync(DirectoryInfo? repoRootArg)
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
            WorkingDirectory = Environment.CurrentDirectory, // Use current dir for git command
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

    // Fallback: An assumed build project is 2 levels deep from the repo root (e.g., repo/build/_build)
    var fallbackPath = new DirectoryPath(AppContext.BaseDirectory).GetParent()?.GetParent()?.Collapse();
    if (fallbackPath != null)
    {
        AnsiConsole.MarkupLine($"[yellow]Warning:[/] Could not determine repo root via git. Assuming relative path: {fallbackPath.FullPath}");
        return fallbackPath;
    }

    // Absolute fallback if path manipulation fails
    var absoluteFallback = new DirectoryPath(Environment.CurrentDirectory);
    AnsiConsole.MarkupLine($"[red]ERROR:[/] Could not determine repo root via git or relative path. Using CWD: {absoluteFallback.FullPath}");
    return absoluteFallback;
}

static string[] GetEffectiveCakeArguments(string[] originalArgs, DirectoryPath repoRoot, InvocationContext invocationContext)
{
    var parseResult = invocationContext.ParseResult;
    var explicitVerbositySet = parseResult.FindResultFor(CakeOptions.VerbosityOption)?.GetValueOrDefault() != null;
    var explicitWorkingPathSet = parseResult.FindResultFor(CakeOptions.WorkingPathOption)?.GetValueOrDefault() != null;

    var isGitHubDebugRun = string.Equals(Environment.GetEnvironmentVariable("ACTIONS_RUNNER_DEBUG"), "true", StringComparison.OrdinalIgnoreCase);

    var shouldInjectVerbosity = isGitHubDebugRun && !explicitVerbositySet;
    var shouldInjectWorkingPath = !explicitWorkingPathSet;

    // Early return if no modifications needed
    if (!shouldInjectVerbosity && !shouldInjectWorkingPath)
    {
        return originalArgs;
    }

    // Mark indices to skip
    var indicesToSkip = new HashSet<int>();
    for (var i = 0; i < originalArgs.Length; i++)
    {
        var arg = originalArgs[i];

        if ((!shouldInjectVerbosity || !IsVerbosityArg(arg)) && (!shouldInjectWorkingPath || !IsWorkingPathArg(arg)))
        {
            continue;
        }

        indicesToSkip.Add(i);
        // Mark the next argument for skipping if it's a value (not a flag)
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

    // Inject new arguments
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

// Helper methods for cleaner arg checking
static bool IsVerbosityArg(string arg) =>
    string.Equals(arg, "--verbosity", StringComparison.OrdinalIgnoreCase) ||
    string.Equals(arg, "-v", StringComparison.OrdinalIgnoreCase);

static bool IsWorkingPathArg(string arg) =>
    string.Equals(arg, "--working", StringComparison.OrdinalIgnoreCase) ||
    string.Equals(arg, "-w", StringComparison.OrdinalIgnoreCase);

namespace Build
{
    public record ParsedArguments(
        DirectoryInfo? RepoRoot,
        string Config,
        DirectoryInfo? VcpkgDir,
        DirectoryInfo? VcpkgInstalledDir,
        IList<string> Library,
        string Rid,
        IList<string> Dll,
        string? Suffix,
        IList<string> Scope,
        IList<string> ExplicitVersion,
        string? ExplicitVersions,
        string? VersionsFile);
}
