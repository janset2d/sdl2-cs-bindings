using Build.Tests.Fixtures;
using Cake.Core;

namespace Build.Tests.Unit.Fixtures;

public sealed class FakeCakeWorldV2PlatformFactoriesTests
{
    [Test]
    public async Task CreateWindows_Should_Set_Windows_Environment()
    {
        var world = FakeCakeWorldV2.CreateWindows();

        await Assert.That((int)world.Environment.Platform.Family).IsEqualTo((int)PlatformFamily.Windows);
    }

    [Test]
    public async Task CreateWindows_Should_Seed_Manifest_On_Disk()
    {
        var world = FakeCakeWorldV2.CreateWindows();

        await Assert.That(world.FileExists("build/manifest.json")).IsTrue();
    }

    [Test]
    public async Task CreateWindows_Should_Seed_Valid_Manifest_Content()
    {
        var world = FakeCakeWorldV2.CreateWindows();

        var content = world.ReadAllText("build/manifest.json");

        await Assert.That(content).Contains("\"schema_version\"");
        await Assert.That(content).Contains("\"win-x64\"");
    }

    [Test]
    public async Task CreateLinux_Should_Set_Unix_Environment()
    {
        var world = FakeCakeWorldV2.CreateLinux();

        await Assert.That((int)world.Environment.Platform.Family).IsEqualTo((int)PlatformFamily.Linux);
    }

    [Test]
    public async Task CreateLinux_Should_Seed_Manifest_With_Linux_RID()
    {
        var world = FakeCakeWorldV2.CreateLinux();

        var content = world.ReadAllText("build/manifest.json");

        await Assert.That(content).Contains("\"linux-x64\"");
    }

    [Test]
    public async Task CreateOsx_Should_Set_Unix_Environment()
    {
        var world = FakeCakeWorldV2.CreateOsx();

        await Assert.That(world.Environment.Platform.Family).IsEqualTo(PlatformFamily.Linux);
        // Cake.Testing only has CreateUnixEnvironment() — no separate macOS factory.
        // CreateOsx uses Unix internally; platform-specific behaviour comes from the
        // seeded manifest fixture, not the environment family.
    }

    [Test]
    public async Task CreateOsx_Should_Seed_Manifest_With_Osx_RID()
    {
        var world = FakeCakeWorldV2.CreateOsx();

        var content = world.ReadAllText("build/manifest.json");

        await Assert.That(content).Contains("\"osx-x64\"");
    }

    [Test]
    public async Task Platform_Factories_Should_Be_Overridable_Via_Fluent_API()
    {
        var world = FakeCakeWorldV2.CreateWindows()
            .WithRid("win-arm64")
            .WithManifestFile("{\"overridden\": true}");

        var content = world.ReadAllText("build/manifest.json");

        await Assert.That(content).IsEqualTo("{\"overridden\": true}");
    }

    [Test]
    public async Task AnsiConsole_Should_Be_NonNull_After_Construction()
    {
        var world = FakeCakeWorldV2.CreateWindows();

        await Assert.That(world.AnsiConsole).IsNotNull();
    }
}
