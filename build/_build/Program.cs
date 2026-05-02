# pragma warning disable CA1031, MA0045, MA0051, CA1502, CA1505

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.NamingConventionBinder;
using System.Diagnostics;
using Build;
using Build.Features.Ci;
using Build.Features.Coverage;
using Build.Features.DependencyAnalysis;
using Build.Features.Diagnostics;
using Build.Features.Harvesting;
using Build.Features.Info;
using Build.Features.LocalDev;
using Build.Features.Maintenance;
using Build.Features.Packaging;
using Build.Features.Preflight;
using Build.Features.Publishing;
using Build.Features.Vcpkg;
using Build.Features.Versioning;
using Build.Host;
using Build.Host.Cake;
using Build.Host.Cli.Options;
using Build.Host.Configuration;
using Build.Integrations;
using Build.Tools;
using Cake.Core;
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

root.AddOption(RepositoryOptions.RepoRooOption);

root.AddOption(DotNetOptions.ConfigOption);

root.AddOption(VcpkgOptions.VcpkgDirOption);
root.AddOption(VcpkgOptions.VcpkgInstalledDirOption);
root.AddOption(VcpkgOptions.LibraryOption);
root.AddOption(VcpkgOptions.RidOption);
root.AddOption(PackageOptions.SourceOption);

root.AddOption(VersioningOptions.VersionSourceOption);
root.AddOption(VersioningOptions.VersionSuffixOption);
root.AddOption(VersioningOptions.VersionScopeOption);
root.AddOption(VersioningOptions.ExplicitVersionOption);
root.AddOption(VersioningOptions.VersionsFileOption);

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

    // Mutually exclusive inputs: either --versions-file for a precomputed mapping or
    // --explicit-version for repeated CLI entries. Never both. File I/O is deferred to
    // ICakeContext.ToJson<T>() so the read flows through Cake's IFileSystem.
    var hasVersionsFile = !string.IsNullOrWhiteSpace(parsedArgs.VersionsFile);
    var hasExplicitVersion = parsedArgs.ExplicitVersion.Any(e => !string.IsNullOrWhiteSpace(e));
    if (hasVersionsFile && hasExplicitVersion)
    {
        throw new InvalidOperationException(
            "--versions-file and --explicit-version are mutually exclusive. Use one or the other.");
    }

    services.AddSingleton<PackageBuildConfiguration>(provider =>
    {
        if (hasVersionsFile)
        {
            var ctx = provider.GetRequiredService<ICakeContext>();
            var dict = ctx.ToJson<Dictionary<string, string>>(new FilePath(parsedArgs.VersionsFile!));
            var entries = dict.Select(kvp => $"{kvp.Key}={kvp.Value}");
            return new PackageBuildConfiguration(ExplicitVersionParser.ParseCliEntries(entries));
        }

        return new PackageBuildConfiguration(ExplicitVersionParser.ParseCliEntries(parsedArgs.ExplicitVersion));
    });
    services.AddSingleton(new VersioningConfiguration(parsedArgs.VersionSource, parsedArgs.Suffix, [.. parsedArgs.Scope]));
    services.AddSingleton(new DumpbinConfiguration([.. parsedArgs.Dll]));

    // BuildOptions aggregate per ADR-004 §2.11.1: composed once at startup from the six
    // operator-input sub-records above. Tasks consume the aggregate via context.Options.X
    // for the canonical surface; services that only need a single axis still inject the
    // sub-record directly.
    services.AddSingleton<BuildOptions>(provider => new BuildOptions(
        Vcpkg: provider.GetRequiredService<VcpkgConfiguration>(),
        Package: provider.GetRequiredService<PackageBuildConfiguration>(),
        Versioning: provider.GetRequiredService<VersioningConfiguration>(),
        Repository: provider.GetRequiredService<RepositoryConfiguration>(),
        DotNet: provider.GetRequiredService<DotNetBuildConfiguration>(),
        Dumpbin: provider.GetRequiredService<DumpbinConfiguration>()));

    var source = parsedArgs.Source?.Trim();
    if (string.IsNullOrWhiteSpace(source))
    {
        throw new InvalidOperationException("--source cannot be empty. Allowed values: local, remote, release.");
    }

    // Composition root reads as the architectural index per ADR-004 §2.12: 13
    // per-feature AddXFeature() calls + 3 cross-cutting groupings (AddHostBuildingBlocks,
    // AddIntegrations, AddToolWrappers). AddPackagingFeature carries the parsed --source
    // CLI value because its resolver factory closure consumes it. AddHostBuildingBlocks
    // takes parsedArgs for the same reason (IPathService consumes vcpkg-dir overrides).
    // The LocalDev orchestration feature is registered last so every sibling pipeline it
    // composes is already in the container (per ADR-004 §2.5 + §2.13 invariant #4 allowlist).
    services
        .AddHostBuildingBlocks(parsedArgs)
        .AddIntegrations()
        .AddToolWrappers()
        .AddInfoFeature()
        .AddMaintenanceFeature()
        .AddCiFeature()
        .AddCoverageFeature()
        .AddVersioningFeature()
        .AddVcpkgFeature()
        .AddDiagnosticsFeature()
        .AddDependencyAnalysisFeature()
        .AddPreflightFeature()
        .AddHarvestingFeature()
        .AddPublishingFeature()
        .AddPackagingFeature(source)
        .AddLocalDevFeature();
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
        string Source,
        string Rid,
        IList<string> Dll,
        string? VersionSource,
        string? Suffix,
        IList<string> Scope,
        IList<string> ExplicitVersion,
        string? VersionsFile);
}
