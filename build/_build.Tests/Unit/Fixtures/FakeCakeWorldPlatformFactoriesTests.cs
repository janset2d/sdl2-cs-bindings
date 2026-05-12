using Build.Tests.Fixtures;
using Cake.Core.IO;
using Cake.Core;

namespace Build.Tests.Unit.Fixtures;

public sealed class FakeCakeWorldPlatformFactoriesTests
{
    [Test]
    public async Task CreateWindows_Should_Set_Windows_Environment()
    {
        var world = FakeCakeWorld.CreateWindows();

        await Assert.That((int)world.Environment.Platform.Family).IsEqualTo((int)PlatformFamily.Windows);
    }

    [Test]
    public async Task CreateWindows_Should_Seed_Manifest_On_Disk()
    {
        var world = FakeCakeWorld.CreateWindows();

        await Assert.That(world.FileExists("build/manifest.json")).IsTrue();
    }

    [Test]
    public async Task CreateWindows_Should_Seed_Valid_Manifest_Content()
    {
        var world = FakeCakeWorld.CreateWindows();

        var content = world.ReadAllText("build/manifest.json");

        await Assert.That(content).Contains("\"schema_version\"");
        await Assert.That(content).Contains("\"win-x64\"");
    }

    [Test]
    public async Task CreateWindows_Should_Not_Seed_Default_Process_Result()
    {
        var world = FakeCakeWorld.CreateWindows();

        var exception = await Assert
            .That(() => world.CakeContext.ProcessRunner.Start(new FilePath("unconfigured"), new ProcessSettings()))
            .Throws<InvalidOperationException>();

        await Assert.That(exception!.Message).Contains("Process 'unconfigured' was not configured");
    }

    [Test]
    public async Task CreateLinux_Should_Set_Unix_Environment()
    {
        var world = FakeCakeWorld.CreateLinux();

        await Assert.That((int)world.Environment.Platform.Family).IsEqualTo((int)PlatformFamily.Linux);
    }

    [Test]
    public async Task CreateLinux_Should_Seed_Manifest_With_Linux_RID()
    {
        var world = FakeCakeWorld.CreateLinux();

        var content = world.ReadAllText("build/manifest.json");

        await Assert.That(content).Contains("\"linux-x64\"");
    }

    [Test]
    public async Task CreateLinux_Should_Not_Seed_Default_Process_Result()
    {
        var world = FakeCakeWorld.CreateLinux();

        var exception = await Assert
            .That(() => world.CakeContext.ProcessRunner.Start(new FilePath("unconfigured"), new ProcessSettings()))
            .Throws<InvalidOperationException>();

        await Assert.That(exception!.Message).Contains("Process 'unconfigured' was not configured");
    }

    [Test]
    public async Task CreateOsx_Should_Set_Unix_Environment()
    {
        var world = FakeCakeWorld.CreateOsx();

        await Assert.That(world.Environment.Platform.Family).IsEqualTo(PlatformFamily.Linux);
        // Cake.Testing only has CreateUnixEnvironment() — no separate macOS factory.
        // CreateOsx uses Unix internally; platform-specific behaviour comes from the
        // seeded manifest fixture, not the environment family.
    }

    [Test]
    public async Task CreateOsx_Should_Seed_Manifest_With_Osx_RID()
    {
        var world = FakeCakeWorld.CreateOsx();

        var content = world.ReadAllText("build/manifest.json");

        await Assert.That(content).Contains("\"osx-x64\"");
    }

    [Test]
    public async Task CreateOsx_Should_Not_Seed_Default_Process_Result()
    {
        var world = FakeCakeWorld.CreateOsx();

        var exception = await Assert
            .That(() => world.CakeContext.ProcessRunner.Start(new FilePath("unconfigured"), new ProcessSettings()))
            .Throws<InvalidOperationException>();

        await Assert.That(exception!.Message).Contains("Process 'unconfigured' was not configured");
    }

    [Test]
    public async Task Platform_Factories_Should_Be_Overridable_Via_Fluent_API()
    {
        var world = FakeCakeWorld.CreateWindows()
            .WithRid("win-arm64")
            .WithManifestFile("{\"overridden\": true}");

        var content = world.ReadAllText("build/manifest.json");

        await Assert.That(content).IsEqualTo("{\"overridden\": true}");
    }

    [Test]
    public async Task AnsiConsole_Should_Be_NonNull_After_Construction()
    {
        var world = FakeCakeWorld.CreateWindows();

        await Assert.That(world.AnsiConsole).IsNotNull();
    }
}
