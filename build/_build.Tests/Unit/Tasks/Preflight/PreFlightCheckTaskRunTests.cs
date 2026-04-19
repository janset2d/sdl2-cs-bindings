using Build.Application.Preflight;
using Build.Context;
using Build.Context.Configs;
using Build.Context.Models;
using Build.Domain.Preflight;
using Build.Domain.Strategy;
using Build.Infrastructure.Vcpkg;
using Build.Tasks.Preflight;
using Build.Tests.Fixtures;
using Cake.Core;

namespace Build.Tests.Unit.Tasks.Preflight;

public class PreFlightCheckTaskRunTests
{
    [Test]
    public void Run_Should_Pass_When_Manifest_And_Vcpkg_Versions_Are_Aligned()
    {
        var manifestConfig = CreateManifestConfig("2.32.10", 0);
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithManifest(manifestConfig)
            .WithVcpkgJson(CreateVcpkgManifest("2.32.10", 0))
            .BuildContextWithHandles();

        var context = repo.BuildContext;

        CreateTask(manifestConfig, context).Run(context);
    }

    [Test]
    public async Task Run_Should_Throw_When_Override_Version_Does_Not_Match_Manifest()
    {
        var manifestConfig = CreateManifestConfig("2.32.10", 0);
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithManifest(manifestConfig)
            .WithVcpkgJson(CreateVcpkgManifest("2.31.0", 0))
            .BuildContextWithHandles();

        var context = repo.BuildContext;

        var task = CreateTask(manifestConfig, context);

        await Assert.That(() => task.Run(context)).Throws<CakeException>();
    }

    [Test]
    public async Task Run_Should_Throw_When_Runtime_Strategy_And_Triplet_Are_Incoherent()
    {
        var manifestConfig = CreateManifestConfig(
            "2.32.10",
            0,
            strategy: "pure-dynamic",
            triplet: "x64-windows-hybrid");
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithManifest(manifestConfig)
            .WithVcpkgJson(CreateVcpkgManifest("2.32.10", 0))
            .BuildContextWithHandles();

        var context = repo.BuildContext;

        var task = CreateTask(manifestConfig, context);

        await Assert.That(() => task.Run(context)).Throws<CakeException>();
    }

    [Test]
    public void Run_Should_Allow_Libraries_Without_Vcpkg_Overrides()
    {
        var manifestConfig = CreateManifestConfig("2.32.10", 0);
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithManifest(manifestConfig)
            .WithTextFile("vcpkg.json", "{\"dependencies\":[\"sdl2\"]}")
            .BuildContextWithHandles();

        var context = repo.BuildContext;

        CreateTask(manifestConfig, context).Run(context);
    }

    [Test]
    public async Task Run_Should_Throw_When_Manifest_Version_Is_Not_A_Valid_Semantic_Version()
    {
        var manifestConfig = CreateManifestConfig("2.32", 0);
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithManifest(manifestConfig)
            .WithVcpkgJson(CreateVcpkgManifest("2.32.10", 0))
            .BuildContextWithHandles();

        var context = repo.BuildContext;

        var task = CreateTask(manifestConfig, context);

        await Assert.That(() => task.Run(context)).Throws<CakeException>();
    }

    [Test]
    public async Task Run_Should_Throw_When_Core_Library_Identity_Drifts_Between_Manifest_Fields()
    {
        var baseline = CreateManifestConfig("2.32.10", 0);
        var manifestConfig = baseline with
        {
            PackagingConfig = baseline.PackagingConfig with { CoreLibrary = "sdl3" },
        };
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithManifest(manifestConfig)
            .WithVcpkgJson(CreateVcpkgManifest("2.32.10", 0))
            .BuildContextWithHandles();

        var context = repo.BuildContext;

        var task = CreateTask(manifestConfig, context);

        var exception = await Assert.That(() => task.Run(context)).Throws<CakeException>();
        await Assert.That(exception!.Message).Contains("core library identity");
        await Assert.That(exception.Message).Contains("sdl2");
        await Assert.That(exception.Message).Contains("sdl3");
    }

    private static ManifestConfig CreateManifestConfig(
        string version,
        int portVersion,
        string strategy = "hybrid-static",
        string triplet = "x64-windows-hybrid")
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
            PackageFamilies = [manifest.PackageFamilies.Single(family => string.Equals(family.Name, "sdl2-core", StringComparison.OrdinalIgnoreCase)) with
            {
                ManagedProject = null,
                NativeProject = null,
            }],
            LibraryManifests =
            [
                ManifestFixture.CreateTestCoreLibrary() with
                {
                    VcpkgVersion = version,
                    VcpkgPortVersion = portVersion,
                },
            ],
        };
    }

    private static VcpkgManifest CreateVcpkgManifest(string version, int portVersion)
    {
        return new VcpkgManifest
        {
            Overrides =
            [
                new VcpkgOverride
                {
                    Name = "sdl2",
                    Version = version,
                    PortVersion = portVersion,
                },
            ],
        };
    }

    private static PreFlightCheckTask CreateTask(ManifestConfig manifestConfig, BuildContext context)
    {
        var packageBuildConfiguration = new PackageBuildConfiguration(["sdl2-core"], null);

        return new PreFlightCheckTask(
            manifestConfig,
            packageBuildConfiguration,
            new VcpkgManifestReader(context.FileSystem),
            new VersionConsistencyValidator(),
            new StrategyCoherenceValidator(new StrategyResolver()),
            new CoreLibraryIdentityValidator(),
            new UpstreamVersionAlignmentValidator(),
            new CsprojPackContractValidator(context.FileSystem),
            new PreflightReporter(context));
    }
}
