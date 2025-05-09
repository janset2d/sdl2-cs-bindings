# pragma warning disable CA1031, MA0045

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.NamingConventionBinder;
using System.Diagnostics;
using Build.Context;
using Build.Context.Configs;
using Build.Context.Models;
using Build.Context.Options;
using Build.Modules;
using Build.Modules.DependencyAnalysis;
using Build.Tools.Dumpbin;
using Cake.Core;
using Cake.Core.Diagnostics;
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
root.AddOption(VcpkgOptions.UseOverridesOption);

root.AddOption(DumpbinOptions.DllOption);

root.Handler = CommandHandler.Create<InvocationContext, ParsedArguments>(RunCakeHostAsync);
return await root.InvokeAsync(args);

static async Task<int> RunCakeHostAsync(InvocationContext context, ParsedArguments parsedArgs)
{
    var repoRootPath = await DetermineRepoRootAsync(parsedArgs.RepoRoot);
    var cakeArgs = context.ParseResult.Tokens.Select(t => t.Value).ToArray();

    return new CakeHost()
        .UseContext<BuildContext>()
        .ConfigureServices(services =>
        {
            services.AddSingleton(new VcpkgConfiguration([.. parsedArgs.Library], parsedArgs.Rid));
            services.AddSingleton(new RepositoryConfiguration(repoRootPath));
            services.AddSingleton(new DotNetBuildConfiguration(configuration: parsedArgs.Config));
            services.AddSingleton(new DumpbinConfiguration([.. parsedArgs.Dll]));

            services.AddSingleton<PathService>(provider =>
            {
                var repositoryConfiguration = provider.GetRequiredService<RepositoryConfiguration>();
                var cakeLogger = provider.GetRequiredService<ICakeLog>();
                return new PathService(repositoryConfiguration, parsedArgs, cakeLogger);
            });

            services.AddSingleton<IDependencyScanner>(provider =>
            {
                var env = provider.GetRequiredService<ICakeEnvironment>();
                var ctx = provider.GetRequiredService<ICakeContext>();

                var rid = env.Platform.Rid();
                return rid switch
                {
                    Rids.WinX64 or Rids.WinX86 or Rids.WinArm64 => new WindowsDumpbinScanner(new DumpbinDependentsTool(ctx)),
                    Rids.LinuxX64 or Rids.LinuxArm64 => new LinuxLddScanner(),
                    Rids.OsxX64 or Rids.OsxArm64 => new MacOtoolScanner(),
                    _ => throw new NotSupportedException("Unsupported OS"),
                };
            });

            services.AddSingleton<RuntimeConfig>(provider =>
            {
                var ctx = provider.GetRequiredService<ICakeContext>();
                var pathService = provider.GetRequiredService<PathService>();

                var runtimesFile = pathService.GetRuntimesFile();
                var runtimeConfig = ctx.ToJson<RuntimeConfig>(runtimesFile);

                return runtimeConfig;
            });

            services.AddSingleton<ManifestConfig>(provider =>
            {
                var ctx = provider.GetRequiredService<ICakeContext>();
                var pathService = provider.GetRequiredService<PathService>();

                var manifestFile = pathService.GetManifestFile();
                var manifestConfig = ctx.ToJson<ManifestConfig>(manifestFile);

                return manifestConfig;
            });

            services.AddSingleton<SystemArtefactsConfig>(provider =>
            {
                var ctx = provider.GetRequiredService<ICakeContext>();
                var pathService = provider.GetRequiredService<PathService>();

                var systemArtifactsFile = pathService.GetSystemArtifactsFile();
                var systemArtefactsConfig = ctx.ToJson<SystemArtefactsConfig>(systemArtifactsFile);

                return systemArtefactsConfig;
            });
        })
        .Run(cakeArgs);
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

    // Fallback: Assume build project is 2 levels deep from repo root (e.g., repo/build/_build)
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

public record ParsedArguments(
    DirectoryInfo? RepoRoot,
    string Config,
    DirectoryInfo? VcpkgDir,
    DirectoryInfo? VcpkgInstalledDir,
    string? Rid,
    IList<string> Library,
    IList<string> Dll);
