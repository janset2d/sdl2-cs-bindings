using Build.Data.Manifest;
using Build.Data.ProjectMetadata;
using Build.Data.Versions;
using Build.Targets.NativeSmoke.Services;
using Build.Targets.Package.Services;
using Build.Targets.PackageConsumerSmoke.Services;
using Build.Tests.Fixtures;
using Build.Validation.Packaging;
using Cake.Core.IO;
using Microsoft.Extensions.DependencyInjection;

namespace Build.Tests.Unit.CompositionRoot;

public sealed class ProgramCompositionRootTests
{
    [Test]
    public async Task IsVerbosityArg_Should_Recognize_Long_And_Short_Flags()
    {
        var longOption = EntryPoint.IsVerbosityArg("--verbosity");
        var shortOption = EntryPoint.IsVerbosityArg("-v");
        var nonVerbosity = EntryPoint.IsVerbosityArg("--target");

        await Assert.That(longOption).IsTrue();
        await Assert.That(shortOption).IsTrue();
        await Assert.That(nonVerbosity).IsFalse();
    }

    [Test]
    public async Task IsWorkingPathArg_Should_Recognize_Long_And_Short_Flags()
    {
        var longOption = EntryPoint.IsWorkingPathArg("--working");
        var shortOption = EntryPoint.IsWorkingPathArg("-w");
        var nonWorking = EntryPoint.IsWorkingPathArg("--target");

        await Assert.That(longOption).IsTrue();
        await Assert.That(shortOption).IsTrue();
        await Assert.That(nonWorking).IsFalse();
    }

    [Test]
    public async Task GetEffectiveCakeArguments_Should_Inject_Working_Path_When_Not_Provided()
    {
        var originalArgs = Array.Empty<string>();
        var repoRoot = new DirectoryPath("C:/repo-root");
        var parseResult = ProgramCompositionRootFixture.CreateParseResult(originalArgs);

        var effectiveArgs = ProgramCompositionRootFixture.RunWithActionsRunnerDebug(
            value: null,
            () => EntryPoint.GetEffectiveCakeArguments(originalArgs, repoRoot, parseResult));

        await Assert.That(effectiveArgs).Contains("--working");
        await Assert.That(effectiveArgs).Contains(repoRoot.FullPath);
        await Assert.That(effectiveArgs).DoesNotContain("--verbosity");
    }

    [Test]
    public async Task GetEffectiveCakeArguments_Should_Inject_Diagnostic_Verbosity_When_ActionsRunnerDebug_Is_True()
    {
        var originalArgs = Array.Empty<string>();
        var repoRoot = new DirectoryPath("C:/repo-root");
        var parseResult = ProgramCompositionRootFixture.CreateParseResult(originalArgs);

        var effectiveArgs = ProgramCompositionRootFixture.RunWithActionsRunnerDebug(
            "true",
            () => EntryPoint.GetEffectiveCakeArguments(originalArgs, repoRoot, parseResult));

        await Assert.That(effectiveArgs).Contains("--verbosity");
        await Assert.That(effectiveArgs).Contains("diagnostic");
        await Assert.That(effectiveArgs).Contains("--working");
    }

    [Test]
    public async Task GetEffectiveCakeArguments_Should_Preserve_Explicit_Working_And_Verbosity()
    {
        var originalArgs = new[] { "--verbosity", "quiet", "--working", "C:/custom" };
        var repoRoot = new DirectoryPath("C:/repo-root");
        var parseResult = ProgramCompositionRootFixture.CreateParseResult(originalArgs);

        var effectiveArgs = ProgramCompositionRootFixture.RunWithActionsRunnerDebug(
            "true",
            () => EntryPoint.GetEffectiveCakeArguments(originalArgs, repoRoot, parseResult));

        await Assert.That(effectiveArgs.Length).IsEqualTo(originalArgs.Length);

        for (var i = 0; i < originalArgs.Length; i++)
        {
            await Assert.That(effectiveArgs[i]).IsEqualTo(originalArgs[i]);
        }
    }

    [Test]
    public async Task DetermineRepoRootAsync_Should_Use_RepoRoot_Argument_When_Path_Exists()
    {
        using var repoRootDirectory = new TempDirectory("janset-repo-root-");
        var repoRoot = new DirectoryPath(repoRootDirectory.Path);

        var result = await EntryPoint.DetermineRepoRootAsync(new DirectoryInfo(repoRoot.FullPath));

        await Assert.That(result.FullPath).IsEqualTo(repoRoot.FullPath);
    }

    [Test]
    public async Task ConfigureBuildServices_Should_Resolve_Core_Production_Services()
    {
        var world = FakeCakeWorld.CreateWindows()
            .WithManifestObject(ProgramCompositionRootFixture.CreateCompositionRootManifest("x64-windows-hybrid"));

        var services = ProgramCompositionRootFixture.CreateServiceCollection(world);
        var parsedArguments = ProgramCompositionRootFixture.CreateParsedArguments(world.RepoRoot.FullPath, "win-x64");

        EntryPoint.ConfigureBuildServices(services, parsedArguments, world.RepoRoot);

        using var provider = services.BuildServiceProvider();

        var hybridStaticOverlayValidator = provider.GetRequiredService<IHybridStaticOverlayValidator>();
        var packageOutputValidator = provider.GetRequiredService<IPackageOutputValidator>();
        var projectMetadataReader = provider.GetRequiredService<IProjectMetadataReader>();
        var manifestRepository = provider.GetRequiredService<IManifestRepository>();
        var versionFileRepository = provider.GetRequiredService<IVersionFileRepository>();
        var dotNetRuntimeEnvironment = provider.GetRequiredService<IDotNetRuntimeEnvironment>();
        var packageFamilyPacker = provider.GetRequiredService<PackageFamilyPacker>();
        var dotNetSmokeRunner = provider.GetRequiredService<DotNetSmokeRunner>();
        var msvcDevEnvironment = provider.GetRequiredService<IMsvcDevEnvironment>();

        await Assert.That(hybridStaticOverlayValidator).IsTypeOf<HybridStaticOverlayValidator>();
        await Assert.That(packageOutputValidator.GetType()).IsEqualTo(typeof(PackageOutputValidator));
        await Assert.That(projectMetadataReader.GetType()).IsEqualTo(typeof(ProjectMetadataReader));
        await Assert.That(manifestRepository.GetType()).IsEqualTo(typeof(ManifestRepository));
        await Assert.That(versionFileRepository.GetType()).IsEqualTo(typeof(VersionFileRepository));
        await Assert.That(dotNetRuntimeEnvironment.GetType()).IsEqualTo(typeof(DotNetRuntimeEnvironment));
        await Assert.That(packageFamilyPacker.GetType()).IsEqualTo(typeof(PackageFamilyPacker));
        await Assert.That(dotNetSmokeRunner).IsNotNull();
        await Assert.That(msvcDevEnvironment.GetType()).IsEqualTo(typeof(MsvcDevEnvironment));
    }

    [Test]
    public async Task ConfigureHostServices_Should_Register_The_Action_Token()
    {
        var world = FakeCakeWorld.CreateWindows();
        var services = ProgramCompositionRootFixture.CreateServiceCollection(world);
        var arguments = ProgramCompositionRootFixture.CreateParsedArguments(world.RepoRoot.FullPath, "win-x64");
        using var cancellation = new CancellationTokenSource();

        EntryPoint.ConfigureHostServices(services, arguments, world.RepoRoot, cancellation.Token);

        using var provider = services.BuildServiceProvider();
        var registeredToken = provider.GetRequiredService<CancellationToken>();
        await Assert.That(registeredToken).IsEqualTo(cancellation.Token);
    }
}
