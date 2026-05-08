# pragma warning disable CA1031, MA0045, MA0051, CA1502, CA1505

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.NamingConventionBinder;
using System.Diagnostics;
using Build;
using Build.Features.Ci;
using Build.Targets.InspectHarvestedDependencies;
using Build.Targets.OtoolAnalyze;
using Build.Features.Harvesting;
using Build.Features.Packaging;
using Build.Features.Preflight;
using Build.Features.Publishing;
using Build.Features.Vcpkg;
using Build.Host;
using Build.Host.Cake;
using Build.Host.Cli.Options;
using Build.Host.Configuration;
using Build.Integrations;
using Build.Repositories;
using Build.Tools;
using Build.Versioning;
using Cake.Core;
using Cake.Core.IO;
using Cake.Frosting;
using Microsoft.Extensions.DependencyInjection;
using NuGet.Versioning;
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

root.AddOption(RepositoryOptions.RepoRooOption);

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

    return new CakeHost()
        .UseContext<BuildContext>()
        .ConfigureServices(services => ConfigureBuildServices(services, parsedArgs, repoRootPath))
        .Run(effectiveCakeArgs);
}

static void ConfigureBuildServices(IServiceCollection services, ParsedArguments parsedArgs, DirectoryPath repoRootPath)
{
    ArgumentNullException.ThrowIfNull(services);
    ArgumentNullException.ThrowIfNull(parsedArgs);

    services.AddSingleton(new VcpkgConfiguration([.. parsedArgs.Library], parsedArgs.Rid));
    services.AddSingleton(new RepositoryConfiguration(repoRootPath));
    services.AddSingleton(new DotNetBuildConfiguration(configuration: parsedArgs.Config));

    // PackageBuildConfiguration carries the resolved family→version mapping consumed by
    // stage targets (PreFlight, Package, ConsumerSmoke, PublishStaging). Populated only
    // from --versions-file. Stage tasks fail-loud at task entry when the mapping is empty.
    // ResolveVersions tasks read named BuildContext properties and emit PackageFamilyVersionSet.
    services.AddSingleton<PackageBuildConfiguration>(provider =>
    {
        var hasVersionsFile = !string.IsNullOrWhiteSpace(parsedArgs.VersionsFile);
        if (!hasVersionsFile)
        {
            return new PackageBuildConfiguration(new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase));
        }

        var ctx = provider.GetRequiredService<ICakeContext>();
        var versionSet = ctx.ToJson<PackageFamilyVersionSet>(new FilePath(parsedArgs.VersionsFile!));
        return new PackageBuildConfiguration(versionSet.ToDictionary(
            static entry => entry.Family.Value,
            static entry => entry.Version,
            StringComparer.OrdinalIgnoreCase));
    });
    services.AddSingleton(new DumpbinConfiguration([.. parsedArgs.Dll]));

    services.AddSingleton<IAnsiConsole>(AnsiConsole.Console);

    // Configurations aggregate: 5 axes (Versioning slot retired in plan v4 — versioning
    // tasks read named BuildContext properties). Tasks consume context.Options.X; services that
    // only need a single axis inject the sub-record directly.
    services.AddSingleton<Configurations>(provider => new Configurations(
        Vcpkg: provider.GetRequiredService<VcpkgConfiguration>(),
        Package: provider.GetRequiredService<PackageBuildConfiguration>(),
        Repository: provider.GetRequiredService<RepositoryConfiguration>(),
        DotNet: provider.GetRequiredService<DotNetBuildConfiguration>(),
        Dumpbin: provider.GetRequiredService<DumpbinConfiguration>()));

    // Composition root: 11 per-feature AddXFeature() calls + 3 cross-cutting groupings
    // (AddHostBuildingBlocks, AddIntegrations, AddToolWrappers). AddHostBuildingBlocks takes
    // parsedArgs because IPathService consumes vcpkg-dir overrides.
    services
        .AddHostBuildingBlocks(parsedArgs)
        .AddRepositories()
        .AddIntegrations()
        .AddToolWrappers()
        .AddCiFeature()
        .AddVcpkgFeature()
        .AddInspectHarvestedDependenciesTarget()
        .AddOtoolAnalyzeTarget()
        .AddPreflightFeature()
        .AddHarvestingFeature()
        .AddPublishingFeature()
        .AddPackagingFeature();
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
