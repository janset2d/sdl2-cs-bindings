using System.CommandLine;
using Build.Data.Manifest.Models;
using Build.Host.Cli;
using Build.Host.Cli.Options;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Microsoft.Extensions.DependencyInjection;

namespace Build.Tests.Fixtures;

internal static class ProgramCompositionRootFixture
{
    private static readonly Lock ParseRootLock = new();
    private static readonly Lock EnvironmentVariableLock = new();
    private static readonly SemaphoreSlim AsyncEnvironmentVariableLock = new(1, 1);
    private static readonly RootCommand ParsingRoot = CreateParsingRoot();

    public static ParseResult CreateHostParseResult(string repoRoot) => CakeHostCommandLine
        .CreateRootCommand((_, _) => Task.FromResult(0))
        .Parse(ProgramCompositionRootData.UserArguments(repoRoot));

    public static ParseResult CreateParseResult(params string[] args)
    {
        lock (ParseRootLock)
        {
            return ParsingRoot.Parse(args);
        }
    }

    public static T RunWithActionsRunnerDebug<T>(string? value, Func<T> callback)
    {
        lock (EnvironmentVariableLock)
        {
            var previous = Environment.GetEnvironmentVariable("ACTIONS_RUNNER_DEBUG");

            try
            {
                Environment.SetEnvironmentVariable("ACTIONS_RUNNER_DEBUG", value);
                return callback();
            }
            finally
            {
                Environment.SetEnvironmentVariable("ACTIONS_RUNNER_DEBUG", previous);
            }
        }
    }

    public static async Task<T> RunWithActionsRunnerDebugAsync<T>(string? value, Func<Task<T>> callback)
    {
        await AsyncEnvironmentVariableLock.WaitAsync();
        var previous = Environment.GetEnvironmentVariable("ACTIONS_RUNNER_DEBUG");

        try
        {
            Environment.SetEnvironmentVariable("ACTIONS_RUNNER_DEBUG", value);
            return await callback();
        }
        finally
        {
            Environment.SetEnvironmentVariable("ACTIONS_RUNNER_DEBUG", previous);
            AsyncEnvironmentVariableLock.Release();
        }
    }

    public static ServiceCollection CreateServiceCollection(FakeCakeWorld world)
    {
        var services = new ServiceCollection();
        services.AddSingleton(world.CakeContext.Environment);
        services.AddSingleton(world.CakeContext);
        services.AddSingleton<IFileSystem>(world.FileSystem);
        services.AddSingleton<ICakeLog>(world.Log);

        return services;
    }

    public static ParsedArguments CreateParsedArguments(string repoRoot, string rid)
    {
        return new ParsedArguments(
            RepoRoot: new DirectoryInfo(repoRoot),
            Config: "Release",
            VcpkgDir: null,
            VcpkgInstalledDir: null,
            Library: [],
            Rid: rid,
            Dll: [],
            Suffix: null,
            Scope: [],
            ExplicitVersion: [],
            ExplicitVersions: null,
            VersionsFile: null);
    }

    public static ManifestConfig CreateCompositionRootManifest(string triplet)
    {
        var manifest = ManifestFixture.CreateTestManifestConfig();

        return manifest with
        {
            Runtimes =
            [
                new RuntimeInfo
                {
                    Rid = "win-x64",
                    Triplet = triplet,
                    Runner = "windows-latest",
                    ContainerImage = null,
                },
            ],
            PackageFamilies = [manifest.PackageFamilies.Single(family => string.Equals(family.Name, "sdl2-core", StringComparison.OrdinalIgnoreCase))],
            LibraryManifests = [ManifestFixture.CreateTestCoreLibrary()],
        };
    }

    private static RootCommand CreateParsingRoot()
    {
        var root = new RootCommand("Program helper parse root for tests");
        root.Options.Add(CakeOptions.VerbosityOption);
        root.Options.Add(CakeOptions.WorkingPathOption);
        return root;
    }
}
