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
using Build.Features.Packaging.ArtifactSourceResolvers;
using Build.Features.Preflight;
using Build.Features.Publishing;
using Build.Features.Vcpkg;
using Build.Features.Versioning;
using Build.Host;
using Build.Host.Cake;
using Build.Host.Cli.Options;
using Build.Host.Configuration;
using Build.Host.Paths;
using Build.Integrations.Coverage;
using Build.Integrations.DependencyAnalysis;
using Build.Integrations.DotNet;
using Build.Integrations.Msvc;
using Build.Integrations.NuGet;
using Build.Integrations.Vcpkg;
using Build.Shared.Manifest;
using Build.Shared.Runtime;
using Build.Shared.Strategy;
using Build.Tools.Vcpkg;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Cake.Frosting;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using DotNetRuntimeEnvironment = Build.Integrations.DotNet.DotNetRuntimeEnvironment;

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

    var source = parsedArgs.Source?.Trim();
    if (string.IsNullOrWhiteSpace(source))
    {
        throw new InvalidOperationException("--source cannot be empty. Allowed values: local, remote, release.");
    }

    services.AddSingleton<IPathService>(provider =>
    {
        var repositoryConfiguration = provider.GetRequiredService<RepositoryConfiguration>();
        var cakeLogger = provider.GetRequiredService<ICakeLog>();
        return new PathService(repositoryConfiguration, parsedArgs, cakeLogger);
    });

    services.AddSingleton<IRuntimeProfile>(sp =>
    {
        var runtimeConfig = sp.GetRequiredService<RuntimeConfig>();
        var systemArtefactsConfig = sp.GetRequiredService<SystemArtefactsConfig>();
        var vcpkgConfiguration = sp.GetRequiredService<VcpkgConfiguration>();
        var cakeEnvironment = sp.GetRequiredService<ICakeEnvironment>();

        var rid = vcpkgConfiguration.Rid
            .Match<string>(
                _ => cakeEnvironment.Platform.Rid(),
                configRid => configRid.Value);

        var runtimeInfo = runtimeConfig.Runtimes.Single(r => string.Equals(r.Rid, rid, StringComparison.Ordinal));

        return new RuntimeProfile(runtimeInfo, systemArtefactsConfig);
    });

    services.AddSingleton<IPackageInfoProvider, VcpkgCliProvider>();
    services.AddSingleton<InfoTaskRunner>();
    services.AddSingleton<IBinaryClosureWalker, BinaryClosureWalker>();
    services.AddSingleton<IArtifactPlanner, ArtifactPlanner>();
    services.AddSingleton<IArtifactDeployer, ArtifactDeployer>();
    services.AddSingleton<HarvestTaskRunner>();
    services.AddSingleton<NativeSmokeTaskRunner>();
    services.AddSingleton<ConsolidateHarvestTaskRunner>();
    services.AddSingleton<CleanArtifactsTaskRunner>();
    services.AddSingleton<CompileSolutionTaskRunner>();
    services.AddSingleton<InspectHarvestedDependenciesTaskRunner>();
    services.AddSingleton<GenerateMatrixTaskRunner>();
    services.AddSingleton<OtoolAnalyzeTaskRunner>();
    services.AddSingleton<ICoberturaReader, CoberturaReader>();
    services.AddSingleton<ICoverageBaselineReader, CoverageBaselineReader>();
    services.AddSingleton<ICoverageThresholdValidator, CoverageThresholdValidator>();
    services.AddSingleton<CoverageCheckTaskRunner>();
    services.AddSingleton<IVcpkgManifestReader, VcpkgManifestReader>();
    services.AddSingleton<EnsureVcpkgDependenciesTaskRunner>();
    services.AddSingleton<IVersionConsistencyValidator, VersionConsistencyValidator>();
    services.AddSingleton<IStrategyResolver, StrategyResolver>();
    services.AddSingleton<IStrategyCoherenceValidator, StrategyCoherenceValidator>();
    services.AddSingleton<ICoreLibraryIdentityValidator, CoreLibraryIdentityValidator>();
    services.AddSingleton<IUpstreamVersionAlignmentValidator, UpstreamVersionAlignmentValidator>();
    services.AddSingleton<ICsprojPackContractValidator, CsprojPackContractValidator>();
    services.AddSingleton<IG58CrossFamilyDepResolvabilityValidator, G58CrossFamilyDepResolvabilityValidator>();
    services.AddSingleton<PreflightReporter>();
    services.AddSingleton<PreflightTaskRunner>();
    services.AddSingleton<NativePackageMetadataValidator>();
    services.AddSingleton<ReadmeMappingTableValidator>();
    services.AddSingleton<IPackageOutputValidator, PackageOutputValidator>();
    services.AddSingleton<IProjectMetadataReader, ProjectMetadataReader>();
    // Stage tasks consume already-resolved explicit versions only. ResolveVersions handles
    // every release shape upstream of stages: manifest+suffix dispatch, explicit dispatch,
    // targeted family-tag push, and meta-tag train push. Downstream jobs feed the resolved
    // versions.json back in via --explicit-version / --versions-file.
    services.AddSingleton<IPackageVersionProvider>(provider =>
    {
        var manifest = provider.GetRequiredService<ManifestConfig>();
        var upstreamVersionAlignmentValidator = provider.GetRequiredService<IUpstreamVersionAlignmentValidator>();
        var packageBuildConfig = provider.GetRequiredService<PackageBuildConfiguration>();
        return new ExplicitVersionProvider(manifest, upstreamVersionAlignmentValidator, packageBuildConfig.ExplicitVersions);
    });
    services.AddSingleton<ResolveVersionsTaskRunner>();
    services.AddSingleton<IDotNetPackInvoker, DotNetPackInvoker>();
    services.AddSingleton<IDotNetRuntimeEnvironment, DotNetRuntimeEnvironment>();
    services.AddSingleton<INuGetFeedClient, NuGetProtocolFeedClient>();
    services.AddSingleton<INativePackageMetadataGenerator, NativePackageMetadataGenerator>();
    services.AddSingleton<IReadmeMappingTableGenerator, ReadmeMappingTableGenerator>();
    services.AddSingleton<IPackageTaskRunner, PackageTaskRunner>();
    services.AddSingleton<IPackageConsumerSmokeRunner, PackageConsumerSmokeRunner>();
    services.AddSingleton<PublishTaskRunner>();
    services.AddSingleton<VcpkgBootstrapTool>();
    services.AddSingleton<IMsvcDevEnvironment, MsvcDevEnvironment>();
    services.AddSingleton<LocalArtifactSourceResolver>();
    services.AddSingleton<RemoteArtifactSourceResolver>();
    services.AddSingleton<ArtifactSourceResolverFactory>();
    services.AddSingleton<IArtifactSourceResolver>(provider =>
        provider.GetRequiredService<ArtifactSourceResolverFactory>().Create(source));
    services.AddSingleton<SetupLocalDevTaskRunner>();

    services.AddSingleton<PackagingStrategyFactory>();
    services.AddSingleton<IPackagingStrategy>(provider =>
        provider.GetRequiredService<PackagingStrategyFactory>().Create());

    services.AddSingleton<DependencyPolicyValidatorFactory>();
    services.AddSingleton<IDependencyPolicyValidator>(provider =>
        provider.GetRequiredService<DependencyPolicyValidatorFactory>().Create());

    services.AddSingleton<IRuntimeScanner>(provider =>
    {
        var env = provider.GetRequiredService<ICakeEnvironment>();
        var context = provider.GetRequiredService<ICakeContext>();

        var currentRid = env.Platform.Rid();
        return currentRid switch
        {
            Rids.WinX64 or Rids.WinX86 or Rids.WinArm64 => new WindowsDumpbinScanner(context),
            Rids.LinuxX64 or Rids.LinuxArm64 => new LinuxLddScanner(context),
            Rids.OsxX64 or Rids.OsxArm64 => new MacOtoolScanner(context),
            _ => throw new NotSupportedException($"Unsupported OS for IRuntimeScanner: {currentRid}"),
        };
    });

    // Single manifest.json load - schema v2.1 merges runtimes + system_exclusions + library_manifests + package_families
    services.AddSingleton<ManifestConfig>(provider =>
    {
        var ctx = provider.GetRequiredService<ICakeContext>();
        var pathService = provider.GetRequiredService<IPathService>();

        var manifestFile = pathService.GetManifestFile();
        return ctx.ToJson<ManifestConfig>(manifestFile);
    });

    services.AddSingleton<RuntimeConfig>(provider =>
    {
        var manifest = provider.GetRequiredService<ManifestConfig>();

        return manifest.Runtimes.Count == 0
            ? throw new InvalidOperationException("manifest.json requires a non-empty runtimes section.")
            : new RuntimeConfig { Runtimes = manifest.Runtimes };
    });

    services.AddSingleton<SystemArtefactsConfig>(provider =>
    {
        var manifest = provider.GetRequiredService<ManifestConfig>();

        return manifest.SystemExclusions;
    });
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
