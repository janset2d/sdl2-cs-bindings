using System.CommandLine;
using System.CommandLine.Invocation;
using System.Reflection;
using Build.Application.Packaging;
using Build.Application.Versioning;
using Build.Context;
using Build.Context.Models;
using Build.Context.Options;
using Build.Domain.Coverage;
using Build.Domain.Packaging;
using Build.Domain.Packaging.Models;
using Build.Domain.Preflight;
using Build.Domain.Runtime;
using Build.Domain.Strategy;
using Build.Domain.Strategy.Models;
using Build.Infrastructure.DotNet;
using Build.Infrastructure.Tools.Msvc;
using Build.Infrastructure.Vcpkg;
using Build.Tests.Fixtures;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Cake.Testing;
using Microsoft.Extensions.DependencyInjection;

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
        var repoRoot = WorkspaceFiles.RepoRoot;

        var task = (Task<DirectoryPath>)method.Invoke(null, [new DirectoryInfo(repoRoot.FullPath)])!;
        var result = await task;

        await Assert.That(result.FullPath).IsEqualTo(repoRoot.FullPath);
    }

    [Test]
    public async Task ConfigureBuildServices_Should_Resolve_Hybrid_Strategy_And_Validator()
    {
        var method = GetProgramHelper(
            "g__ConfigureBuildServices",
            typeof(IServiceCollection),
            typeof(ParsedArguments),
            typeof(DirectoryPath));

        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithManifest(CreateCompositionRootManifest("hybrid-static", "x64-windows-hybrid"))
            .BuildContextWithHandles();

        var services = CreateServiceCollectionForCompositionRoot(repo);
        var parsedArguments = CreateParsedArguments(repo.RepoRoot.FullPath, "win-x64");

        method.Invoke(null, [services, parsedArguments, repo.RepoRoot]);

        using var provider = services.BuildServiceProvider();

        var strategy = provider.GetRequiredService<IPackagingStrategy>();
        var strategyResolver = provider.GetRequiredService<IStrategyResolver>();
        var validator = provider.GetRequiredService<IDependencyPolicyValidator>();
        var coverageThresholdValidator = provider.GetRequiredService<ICoverageThresholdValidator>();
        var vcpkgManifestReader = provider.GetRequiredService<IVcpkgManifestReader>();
        var versionConsistencyValidator = provider.GetRequiredService<IVersionConsistencyValidator>();
        var strategyCoherenceValidator = provider.GetRequiredService<IStrategyCoherenceValidator>();
        var packageOutputValidator = provider.GetRequiredService<IPackageOutputValidator>();
        var projectMetadataReader = provider.GetRequiredService<IProjectMetadataReader>();
        var packageVersionProvider = provider.GetRequiredService<IPackageVersionProvider>();
        var dotNetPackInvoker = provider.GetRequiredService<IDotNetPackInvoker>();
        var dotNetRuntimeEnvironment = provider.GetRequiredService<IDotNetRuntimeEnvironment>();
        var packageTaskRunner = provider.GetRequiredService<IPackageTaskRunner>();
        var packageConsumerSmokeRunner = provider.GetRequiredService<IPackageConsumerSmokeRunner>();
        var msvcDevEnvironment = provider.GetRequiredService<IMsvcDevEnvironment>();

        await Assert.That(strategy.Model).IsEqualTo(PackagingModel.HybridStatic);
        await Assert.That(strategy.GetType()).IsEqualTo(typeof(HybridStaticStrategy));
        await Assert.That(strategyResolver.GetType()).IsEqualTo(typeof(StrategyResolver));
        await Assert.That(validator.GetType()).IsEqualTo(typeof(HybridStaticValidator));
        await Assert.That(coverageThresholdValidator.GetType()).IsEqualTo(typeof(CoverageThresholdValidator));
        await Assert.That(vcpkgManifestReader.GetType()).IsEqualTo(typeof(VcpkgManifestReader));
        await Assert.That(versionConsistencyValidator.GetType()).IsEqualTo(typeof(VersionConsistencyValidator));
        await Assert.That(strategyCoherenceValidator.GetType()).IsEqualTo(typeof(StrategyCoherenceValidator));
        await Assert.That(packageOutputValidator.GetType()).IsEqualTo(typeof(PackageOutputValidator));
        await Assert.That(projectMetadataReader.GetType()).IsEqualTo(typeof(ProjectMetadataReader));
        await Assert.That(packageVersionProvider.GetType()).IsEqualTo(typeof(ExplicitVersionProvider));
        await Assert.That(dotNetPackInvoker.GetType()).IsEqualTo(typeof(DotNetPackInvoker));
        await Assert.That(dotNetRuntimeEnvironment.GetType()).IsEqualTo(typeof(DotNetRuntimeEnvironment));
        await Assert.That(packageTaskRunner.GetType()).IsEqualTo(typeof(PackageTaskRunner));
        await Assert.That(packageConsumerSmokeRunner.GetType()).IsEqualTo(typeof(PackageConsumerSmokeRunner));
        await Assert.That(msvcDevEnvironment.GetType()).IsEqualTo(typeof(MsvcDevEnvironment));
    }

    [Test]
    public async Task ConfigureBuildServices_Should_Resolve_PureDynamic_Strategy_And_Validator()
    {
        var method = GetProgramHelper(
            "g__ConfigureBuildServices",
            typeof(IServiceCollection),
            typeof(ParsedArguments),
            typeof(DirectoryPath));

        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithManifest(CreateCompositionRootManifest("pure-dynamic", "x64-windows"))
            .BuildContextWithHandles();

        var services = CreateServiceCollectionForCompositionRoot(repo);
        var parsedArguments = CreateParsedArguments(repo.RepoRoot.FullPath, "win-x64");

        method.Invoke(null, [services, parsedArguments, repo.RepoRoot]);

        using var provider = services.BuildServiceProvider();

        var strategy = provider.GetRequiredService<IPackagingStrategy>();
        var strategyResolver = provider.GetRequiredService<IStrategyResolver>();
        var validator = provider.GetRequiredService<IDependencyPolicyValidator>();
        var coverageThresholdValidator = provider.GetRequiredService<ICoverageThresholdValidator>();
        var vcpkgManifestReader = provider.GetRequiredService<IVcpkgManifestReader>();
        var packageOutputValidator = provider.GetRequiredService<IPackageOutputValidator>();
        var projectMetadataReader = provider.GetRequiredService<IProjectMetadataReader>();
        var packageVersionProvider = provider.GetRequiredService<IPackageVersionProvider>();
        var dotNetPackInvoker = provider.GetRequiredService<IDotNetPackInvoker>();
        var packageTaskRunner = provider.GetRequiredService<IPackageTaskRunner>();
        var packageConsumerSmokeRunner = provider.GetRequiredService<IPackageConsumerSmokeRunner>();

        await Assert.That(strategy.Model).IsEqualTo(PackagingModel.PureDynamic);
        await Assert.That(strategy.GetType()).IsEqualTo(typeof(PureDynamicStrategy));
        await Assert.That(strategyResolver.GetType()).IsEqualTo(typeof(StrategyResolver));
        await Assert.That(validator.GetType()).IsEqualTo(typeof(PureDynamicValidator));
        await Assert.That(coverageThresholdValidator.GetType()).IsEqualTo(typeof(CoverageThresholdValidator));
        await Assert.That(vcpkgManifestReader.GetType()).IsEqualTo(typeof(VcpkgManifestReader));
        await Assert.That(packageOutputValidator.GetType()).IsEqualTo(typeof(PackageOutputValidator));
        await Assert.That(projectMetadataReader.GetType()).IsEqualTo(typeof(ProjectMetadataReader));
        await Assert.That(packageVersionProvider.GetType()).IsEqualTo(typeof(ExplicitVersionProvider));
        await Assert.That(dotNetPackInvoker.GetType()).IsEqualTo(typeof(DotNetPackInvoker));
        await Assert.That(packageTaskRunner.GetType()).IsEqualTo(typeof(PackageTaskRunner));
        await Assert.That(packageConsumerSmokeRunner.GetType()).IsEqualTo(typeof(PackageConsumerSmokeRunner));
    }

    [Test]
    public async Task ConfigureBuildServices_Should_Resolve_Remote_Source_Resolver()
    {
        var method = GetProgramHelper(
            "g__ConfigureBuildServices",
            typeof(IServiceCollection),
            typeof(ParsedArguments),
            typeof(DirectoryPath));

        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithManifest(CreateCompositionRootManifest("hybrid-static", "x64-windows-hybrid"))
            .BuildContextWithHandles();

        var services = CreateServiceCollectionForCompositionRoot(repo);
        var parsedArguments = CreateParsedArguments(repo.RepoRoot.FullPath, "win-x64", "remote");

        method.Invoke(null, [services, parsedArguments, repo.RepoRoot]);

        using var provider = services.BuildServiceProvider();
        var resolver = provider.GetRequiredService<IArtifactSourceResolver>();

        await Assert.That(resolver.GetType()).IsEqualTo(typeof(UnsupportedArtifactSourceResolver));
        await Assert.That(resolver.Profile).IsEqualTo(ArtifactProfile.RemoteInternal);
    }

    [Test]
    public async Task ConfigureBuildServices_Should_Resolve_Release_Source_Resolver()
    {
        var method = GetProgramHelper(
            "g__ConfigureBuildServices",
            typeof(IServiceCollection),
            typeof(ParsedArguments),
            typeof(DirectoryPath));

        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithManifest(CreateCompositionRootManifest("hybrid-static", "x64-windows-hybrid"))
            .BuildContextWithHandles();

        var services = CreateServiceCollectionForCompositionRoot(repo);
        var parsedArguments = CreateParsedArguments(repo.RepoRoot.FullPath, "win-x64", "release");

        method.Invoke(null, [services, parsedArguments, repo.RepoRoot]);

        using var provider = services.BuildServiceProvider();
        var resolver = provider.GetRequiredService<IArtifactSourceResolver>();

        await Assert.That(resolver.GetType()).IsEqualTo(typeof(UnsupportedArtifactSourceResolver));
        await Assert.That(resolver.Profile).IsEqualTo(ArtifactProfile.ReleasePublic);
    }

    [Test]
    public async Task ConfigureBuildServices_Should_Throw_When_Source_Is_Whitespace()
    {
        var method = GetProgramHelper(
            "g__ConfigureBuildServices",
            typeof(IServiceCollection),
            typeof(ParsedArguments),
            typeof(DirectoryPath));

        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithManifest(CreateCompositionRootManifest("hybrid-static", "x64-windows-hybrid"))
            .BuildContextWithHandles();

        var services = CreateServiceCollectionForCompositionRoot(repo);
        var parsedArguments = CreateParsedArguments(repo.RepoRoot.FullPath, "win-x64", "   ");

        var thrown = await Assert.That(() => method.Invoke(null, [services, parsedArguments, repo.RepoRoot]))
            .Throws<TargetInvocationException>();

        await Assert.That(thrown!.InnerException).IsNotNull();
        await Assert.That(thrown.InnerException!.Message).Contains("--source cannot be empty");
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

    private static ServiceCollection CreateServiceCollectionForCompositionRoot(FakeRepoHandles repo)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ICakeEnvironment>(repo.Environment);
        services.AddSingleton<ICakeContext>(repo.CakeContext);
        services.AddSingleton<IFileSystem>(repo.FileSystem);
        services.AddSingleton<ICakeLog>(new FakeLog());

        return services;
    }

    private static ParsedArguments CreateParsedArguments(string repoRoot, string rid, string source = "local")
    {
        return new ParsedArguments(
            RepoRoot: new DirectoryInfo(repoRoot),
            Config: "Release",
            VcpkgDir: null,
            VcpkgInstalledDir: null,
            Library: [],
            Source: source,
            Rid: rid,
            Dll: [],
            VersionSource: null,
            Suffix: null,
            Scope: [],
            ExplicitVersion: [],
            VersionsFile: null);
    }

    private static ManifestConfig CreateCompositionRootManifest(string strategy, string triplet)
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
                    Strategy = strategy,
                    Runner = "windows-latest",
                    ContainerImage = null,
                },
            ],
            PackageFamilies = [manifest.PackageFamilies.Single(family => string.Equals(family.Name, "sdl2-core", StringComparison.OrdinalIgnoreCase))],
            LibraryManifests = [ManifestFixture.CreateTestCoreLibrary()],
        };
    }
}
