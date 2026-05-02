using Build.Features.Preflight;
using Build.Host;
using Build.Host.Configuration;
using Build.Integrations.Vcpkg;
using Build.Shared.Manifest;
using Build.Shared.Packaging;
using Build.Shared.Strategy;
using Build.Tests.Fixtures;
using Cake.Core;
using NuGet.Versioning;

namespace Build.Tests.Unit.Features.Preflight;

public class PreFlightCheckTaskRunTests
{
    [Test]
    public async Task RunAsync_Should_Pass_When_Manifest_And_Vcpkg_Versions_Are_Aligned()
    {
        var manifestConfig = CreateManifestConfig("2.32.10", 0);
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithManifest(manifestConfig)
            .WithVcpkgJson(CreateVcpkgManifest("2.32.10", 0))
            .BuildContextWithHandles();

        var context = repo.BuildContext;

        await CreateTask(manifestConfig, context).RunAsync(context);
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_Override_Version_Does_Not_Match_Manifest()
    {
        var manifestConfig = CreateManifestConfig("2.32.10", 0);
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithManifest(manifestConfig)
            .WithVcpkgJson(CreateVcpkgManifest("2.31.0", 0))
            .BuildContextWithHandles();

        var context = repo.BuildContext;

        var task = CreateTask(manifestConfig, context);

        await Assert.That(() => task.RunAsync(context)).Throws<CakeException>();
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_Runtime_Strategy_And_Triplet_Are_Incoherent()
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

        await Assert.That(() => task.RunAsync(context)).Throws<CakeException>();
    }

    [Test]
    public async Task RunAsync_Should_Allow_Libraries_Without_Vcpkg_Overrides()
    {
        var manifestConfig = CreateManifestConfig("2.32.10", 0);
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithManifest(manifestConfig)
            .WithTextFile("vcpkg.json", "{\"dependencies\":[\"sdl2\"]}")
            .BuildContextWithHandles();

        var context = repo.BuildContext;

        await CreateTask(manifestConfig, context).RunAsync(context);
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_Manifest_Version_Is_Not_A_Valid_Semantic_Version()
    {
        var manifestConfig = CreateManifestConfig("2.32", 0);
        var repo = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithManifest(manifestConfig)
            .WithVcpkgJson(CreateVcpkgManifest("2.32.10", 0))
            .BuildContextWithHandles();

        var context = repo.BuildContext;

        var task = CreateTask(manifestConfig, context);

        await Assert.That(() => task.RunAsync(context)).Throws<CakeException>();
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_Core_Library_Identity_Drifts_Between_Manifest_Fields()
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

        var exception = await Assert.That(() => task.RunAsync(context)).Throws<CakeException>();
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
        var packageBuildConfiguration = new PackageBuildConfiguration(new Dictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase));

        var runner = new PreflightPipeline(
            manifestConfig,
            new VcpkgManifestReader(context.FileSystem),
            new VersionConsistencyValidator(),
            new StrategyCoherenceValidator(new StrategyResolver()),
            new CoreLibraryIdentityValidator(),
            new UpstreamVersionAlignmentValidator(),
            new CsprojPackContractValidator(context.FileSystem),
            new G58CrossFamilyDepResolvabilityValidator(),
            new PreflightReporter(context));

        return new PreFlightCheckTask(runner, packageBuildConfiguration);
    }
}
