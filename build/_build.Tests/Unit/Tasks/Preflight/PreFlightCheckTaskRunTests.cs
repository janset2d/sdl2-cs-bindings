using Build.Context.Models;
using Build.Tasks.Preflight;
using Build.Tests.Fixtures;

namespace Build.Tests.Unit.Tasks.Preflight;

public class PreFlightCheckTaskRunTests
{
    [Test]
    public void Run_Should_Pass_When_Manifest_And_Vcpkg_Versions_Are_Aligned()
    {
        var context = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithManifest(CreateManifestConfig("2.32.10", 0))
            .WithVcpkgJson(CreateVcpkgManifest("2.32.10", 0))
            .BuildContext();

        new PreFlightCheckTask().Run(context);
    }

    [Test]
    public async Task Run_Should_Throw_When_Override_Version_Does_Not_Match_Manifest()
    {
        var context = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithManifest(CreateManifestConfig("2.32.10", 0))
            .WithVcpkgJson(CreateVcpkgManifest("2.31.0", 0))
            .BuildContext();

        var task = new PreFlightCheckTask();

        await Assert.That(() => task.Run(context)).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Run_Should_Throw_When_Runtime_Strategy_And_Triplet_Are_Incoherent()
    {
        var context = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithManifest(CreateManifestConfig(
                "2.32.10",
                0,
                strategy: "pure-dynamic",
                triplet: "x64-windows-hybrid"))
            .WithVcpkgJson(CreateVcpkgManifest("2.32.10", 0))
            .BuildContext();

        var task = new PreFlightCheckTask();

        await Assert.That(() => task.Run(context)).Throws<InvalidOperationException>();
    }

    [Test]
    public void Run_Should_Allow_Libraries_Without_Vcpkg_Overrides()
    {
        var context = new FakeRepoBuilder(FakeRepoPlatform.Windows)
            .WithManifest(CreateManifestConfig("2.32.10", 0))
            .WithTextFile("vcpkg.json", "{\"dependencies\":[\"sdl2\"]}")
            .BuildContext();

        new PreFlightCheckTask().Run(context);
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
            PackageFamilies = [manifest.PackageFamilies.Single(family => string.Equals(family.Name, "core", StringComparison.OrdinalIgnoreCase))],
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
}
