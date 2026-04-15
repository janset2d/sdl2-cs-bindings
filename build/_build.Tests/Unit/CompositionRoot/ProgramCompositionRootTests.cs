using System.CommandLine;
using System.CommandLine.Invocation;
using System.Reflection;
using Build.Context;
using Build.Context.Options;
using Build.Modules.Contracts;
using Build.Modules.Strategy;
using Build.Modules.Strategy.Models;
using Cake.Core;
using Cake.Core.Configuration;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Cake.Core.Tooling;
using Cake.Testing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace Build.Tests.Unit.CompositionRoot;

public sealed class ProgramCompositionRootTests
{
    private static readonly Lock ParseRootLock = new();
    private static readonly Lock EnvironmentVariableLock = new();
    private static readonly RootCommand ParsingRoot = CreateParsingRoot();

    private static readonly Type ProgramType = typeof(BuildContext).Assembly.GetType("Program")
                                               ?? throw new InvalidOperationException("Unable to resolve Program type from Build assembly.");

    [Test]
    public async Task IsVerbosityArg_Should_Recognize_Long_And_Short_Flags()
    {
        var method = GetProgramHelper("g__IsVerbosityArg", typeof(string));

        var longOption = (bool)method.Invoke(null, ["--verbosity"])!;
        var shortOption = (bool)method.Invoke(null, ["-v"])!;
        var nonVerbosity = (bool)method.Invoke(null, ["--target"])!;

        await Assert.That(longOption).IsTrue();
        await Assert.That(shortOption).IsTrue();
        await Assert.That(nonVerbosity).IsFalse();
    }

    [Test]
    public async Task IsWorkingPathArg_Should_Recognize_Long_And_Short_Flags()
    {
        var method = GetProgramHelper("g__IsWorkingPathArg", typeof(string));

        var longOption = (bool)method.Invoke(null, ["--working"])!;
        var shortOption = (bool)method.Invoke(null, ["-w"])!;
        var nonWorking = (bool)method.Invoke(null, ["--target"])!;

        await Assert.That(longOption).IsTrue();
        await Assert.That(shortOption).IsTrue();
        await Assert.That(nonWorking).IsFalse();
    }

    [Test]
    public async Task GetEffectiveCakeArguments_Should_Inject_Working_Path_When_Not_Provided()
    {
        var method = GetProgramHelper(
            "g__GetEffectiveCakeArguments",
            typeof(string[]),
            typeof(DirectoryPath),
            typeof(InvocationContext));
        var originalArgs = Array.Empty<string>();
        var repoRoot = new DirectoryPath("C:/repo-root");
        var invocationContext = CreateInvocationContext(originalArgs);

        var effectiveArgs = RunWithActionsRunnerDebug(
            value: null,
            () => (string[])method.Invoke(null, [originalArgs, repoRoot, invocationContext])!);

        await Assert.That(effectiveArgs).Contains("--working");
        await Assert.That(effectiveArgs).Contains(repoRoot.FullPath);
        await Assert.That(effectiveArgs).DoesNotContain("--verbosity");
    }

    [Test]
    public async Task GetEffectiveCakeArguments_Should_Inject_Diagnostic_Verbosity_When_ActionsRunnerDebug_Is_True()
    {
        var method = GetProgramHelper(
            "g__GetEffectiveCakeArguments",
            typeof(string[]),
            typeof(DirectoryPath),
            typeof(InvocationContext));
        var originalArgs = Array.Empty<string>();
        var repoRoot = new DirectoryPath("C:/repo-root");
        var invocationContext = CreateInvocationContext(originalArgs);

        var effectiveArgs = RunWithActionsRunnerDebug(
            "true",
            () => (string[])method.Invoke(null, [originalArgs, repoRoot, invocationContext])!);

        await Assert.That(effectiveArgs).Contains("--verbosity");
        await Assert.That(effectiveArgs).Contains("diagnostic");
        await Assert.That(effectiveArgs).Contains("--working");
    }

    [Test]
    public async Task GetEffectiveCakeArguments_Should_Preserve_Explicit_Working_And_Verbosity()
    {
        var method = GetProgramHelper(
            "g__GetEffectiveCakeArguments",
            typeof(string[]),
            typeof(DirectoryPath),
            typeof(InvocationContext));
        var originalArgs = new[] { "--verbosity", "quiet", "--working", "C:/custom" };
        var repoRoot = new DirectoryPath("C:/repo-root");
        var invocationContext = CreateInvocationContext(originalArgs);

        var effectiveArgs = RunWithActionsRunnerDebug(
            "true",
            () => (string[])method.Invoke(null, [originalArgs, repoRoot, invocationContext])!);

        await Assert.That(effectiveArgs.Length).IsEqualTo(originalArgs.Length);
        for (var i = 0; i < originalArgs.Length; i++)
        {
            await Assert.That(effectiveArgs[i]).IsEqualTo(originalArgs[i]);
        }
    }

    [Test]
    public async Task DetermineRepoRootAsync_Should_Use_RepoRoot_Argument_When_Path_Exists()
    {
        var method = GetProgramHelper("g__DetermineRepoRootAsync", typeof(DirectoryInfo));
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "build-program-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);

        try
        {
            var task = (Task<DirectoryPath>)method.Invoke(null, [new DirectoryInfo(path)])!;
            var result = await task;

            await Assert.That(result.FullPath).IsEqualTo(new DirectoryPath(path).FullPath);
        }
        finally
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
    }

    [Test]
    public async Task ConfigureBuildServices_Should_Resolve_Hybrid_Strategy_And_Validator()
    {
        var method = GetProgramHelper(
            "g__ConfigureBuildServices",
            typeof(IServiceCollection),
            typeof(ParsedArguments),
            typeof(DirectoryPath));

        var repoRoot = CreateTempRepoRoot();
        try
        {
            await WriteManifestJsonAsync(repoRoot, "hybrid-static", "x64-windows-hybrid");

            var services = CreateServiceCollectionForCompositionRoot();
            var parsedArguments = CreateParsedArguments(repoRoot, "win-x64");

            method.Invoke(null, [services, parsedArguments, new DirectoryPath(repoRoot)]);

            using var provider = services.BuildServiceProvider();

            var strategy = provider.GetRequiredService<IPackagingStrategy>();
            var validator = provider.GetRequiredService<IDependencyPolicyValidator>();

            await Assert.That(strategy.Model).IsEqualTo(PackagingModel.HybridStatic);
            await Assert.That(strategy.GetType()).IsEqualTo(typeof(HybridStaticStrategy));
            await Assert.That(validator.GetType()).IsEqualTo(typeof(HybridStaticValidator));
        }
        finally
        {
            DeleteDirectoryQuietly(repoRoot);
        }
    }

    [Test]
    public async Task ConfigureBuildServices_Should_Resolve_PureDynamic_Strategy_And_Validator()
    {
        var method = GetProgramHelper(
            "g__ConfigureBuildServices",
            typeof(IServiceCollection),
            typeof(ParsedArguments),
            typeof(DirectoryPath));

        var repoRoot = CreateTempRepoRoot();
        try
        {
            await WriteManifestJsonAsync(repoRoot, "pure-dynamic", "x64-windows");

            var services = CreateServiceCollectionForCompositionRoot();
            var parsedArguments = CreateParsedArguments(repoRoot, "win-x64");

            method.Invoke(null, [services, parsedArguments, new DirectoryPath(repoRoot)]);

            using var provider = services.BuildServiceProvider();

            var strategy = provider.GetRequiredService<IPackagingStrategy>();
            var validator = provider.GetRequiredService<IDependencyPolicyValidator>();

            await Assert.That(strategy.Model).IsEqualTo(PackagingModel.PureDynamic);
            await Assert.That(strategy.GetType()).IsEqualTo(typeof(PureDynamicStrategy));
            await Assert.That(validator.GetType()).IsEqualTo(typeof(PureDynamicValidator));
        }
        finally
        {
            DeleteDirectoryQuietly(repoRoot);
        }
    }

    private static MethodInfo GetProgramHelper(string methodNameFragment, params Type[] parameterTypes)
    {
        var method = ProgramType
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .SingleOrDefault(m =>
                m.Name.Contains(methodNameFragment, StringComparison.Ordinal) &&
                HasParameterSignature(m, parameterTypes));

        return method ?? throw new InvalidOperationException($"Program helper containing '{methodNameFragment}' was not found.");
    }

    private static bool HasParameterSignature(MethodInfo method, params Type[] parameterTypes)
    {
        var parameters = method.GetParameters();
        if (parameters.Length != parameterTypes.Length)
        {
            return false;
        }

        for (var i = 0; i < parameterTypes.Length; i++)
        {
            if (parameters[i].ParameterType != parameterTypes[i])
            {
                return false;
            }
        }

        return true;
    }

    private static InvocationContext CreateInvocationContext(params string[] args)
    {
        lock (ParseRootLock)
        {
            var parseResult = ParsingRoot.Parse(args);
            return new InvocationContext(parseResult);
        }
    }

    private static T RunWithActionsRunnerDebug<T>(string? value, Func<T> callback)
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

    private static RootCommand CreateParsingRoot()
    {
        var root = new RootCommand("Program helper parse root for tests");
        root.AddOption(CakeOptions.VerbosityOption);
        root.AddOption(CakeOptions.WorkingPathOption);
        return root;
    }

        private static ServiceCollection CreateServiceCollectionForCompositionRoot()
        {
                var services = new ServiceCollection();
                var environment = FakeEnvironment.CreateWindowsEnvironment();
                var fileSystem = new FileSystem();
                var context = CreateCakeContext(environment, fileSystem);

                services.AddSingleton<ICakeEnvironment>(environment);
                services.AddSingleton<ICakeContext>(context);
                services.AddSingleton<ICakeLog>(new FakeLog());

                return services;
        }

        private static ICakeContext CreateCakeContext(ICakeEnvironment environment, IFileSystem fileSystem)
        {
                var globber = new Globber(fileSystem, environment);

                var cakeContext = Substitute.For<ICakeContext>();
                cakeContext.Log.Returns(new FakeLog());
                cakeContext.Environment.Returns(environment);
                cakeContext.FileSystem.Returns(fileSystem);
                cakeContext.Globber.Returns(globber);
                cakeContext.Arguments.Returns(Substitute.For<ICakeArguments>());
                cakeContext.Configuration.Returns(Substitute.For<ICakeConfiguration>());
                cakeContext.Data.Returns(Substitute.For<ICakeDataResolver>());
                cakeContext.ProcessRunner.Returns(Substitute.For<IProcessRunner>());
                cakeContext.Registry.Returns(Substitute.For<IRegistry>());
                cakeContext.Tools.Returns(Substitute.For<IToolLocator>());

                return cakeContext;
        }

        private static ParsedArguments CreateParsedArguments(string repoRoot, string rid)
        {
                return new ParsedArguments(
                        RepoRoot: new DirectoryInfo(repoRoot),
                        Config: "Release",
                        VcpkgDir: null,
                        VcpkgInstalledDir: null,
                        Library: [],
                        Rid: rid,
                        Dll: []);
        }

        private static async Task WriteManifestJsonAsync(string repoRoot, string strategy, string triplet)
        {
                var buildDir = System.IO.Path.Combine(repoRoot, "build");
                Directory.CreateDirectory(buildDir);

                var manifestJson = $$"""
                {
                    "schema_version": "2.1",
                    "packaging_config": {
                        "validation_mode": "strict",
                        "core_library": "sdl2"
                    },
                    "runtimes": [
                        {
                            "rid": "win-x64",
                            "triplet": "{{triplet}}",
                            "strategy": "{{strategy}}",
                            "runner": "windows-latest",
                            "container_image": null
                        }
                    ],
                    "package_families": [
                        {
                            "name": "core",
                            "tag_prefix": "core",
                            "managed_project": "src/SDL2.Core/SDL2.Core.csproj",
                            "native_project": "src/native/SDL2.Core.Native/SDL2.Core.Native.csproj",
                            "library_ref": "SDL2",
                            "depends_on": [],
                            "change_paths": [
                                "src/SDL2.Core/**"
                            ]
                        }
                    ],
                    "system_exclusions": {
                        "windows": {
                            "system_dlls": ["kernel32.dll"]
                        },
                        "linux": {
                            "system_libraries": ["libc.so*"]
                        },
                        "osx": {
                            "system_libraries": ["libSystem.B.dylib"]
                        }
                    },
                    "library_manifests": [
                        {
                            "name": "SDL2",
                            "vcpkg_name": "sdl2",
                            "vcpkg_version": "2.32.10",
                            "vcpkg_port_version": 0,
                            "native_lib_name": "SDL2.Core.Native",
                            "native_lib_version": "2.32.10.0",
                            "core_lib": true,
                            "primary_binaries": [
                                {
                                    "os": "Windows",
                                    "patterns": ["SDL2.dll"]
                                }
                            ]
                        }
                    ]
                }
                """;

                await File.WriteAllTextAsync(System.IO.Path.Combine(buildDir, "manifest.json"), manifestJson);
        }

        private static string CreateTempRepoRoot()
        {
                var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "build-program-tests", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(path);
                return path;
        }

        private static void DeleteDirectoryQuietly(string path)
        {
                try
                {
                        if (Directory.Exists(path))
                        {
                                Directory.Delete(path, recursive: true);
                        }
                }
                catch (IOException)
                {
                        // Best effort cleanup for temp test directories.
                }
                catch (UnauthorizedAccessException)
                {
                        // Best effort cleanup for temp test directories.
                }
        }
}
