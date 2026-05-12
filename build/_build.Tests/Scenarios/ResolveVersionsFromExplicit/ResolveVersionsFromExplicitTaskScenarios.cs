using Build.Data;
using Build.Targets.ResolveVersionsFromExplicit;
using Build.Tests.Fixtures;
using Build.Validation.Versioning;
using Microsoft.Extensions.DependencyInjection;

namespace Build.Tests.Scenarios.ResolveVersionsFromExplicit;

public sealed class ResolveVersionsFromExplicitTaskScenarios
{
    [Test]
    public async Task RunAsync_Should_Write_Full_Mapping_From_Operator_Input()
    {
        var world = FakeCakeWorld.CreateWindows()
            .WithManifestObject(ManifestFixture.CreateTestManifestConfig())
            .WithExplicitVersion("sdl2-image=2.8.0-rc.2", "sdl2-core=2.32.0-rc.1")
            .WithVersionsFile("artifacts/resolve-versions/versions.json");

        var result = await CreateHost(world).RunAsync();

        await Assert.That(result.Success).IsTrue();
        var json = world.ReadAllText("artifacts/resolve-versions/versions.json");
        await Assert.That(json).Contains("\"sdl2-core\": \"2.32.0-rc.1\"");
        await Assert.That(json).Contains("\"sdl2-image\": \"2.8.0-rc.2\"");
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_Operator_Mapping_Fails_G54_Alignment()
    {
        var world = FakeCakeWorld.CreateWindows()
            .WithManifestObject(ManifestFixture.CreateTestManifestConfig())
            .WithExplicitVersion("sdl2-core=3.0.0")
            .WithVersionsFile("artifacts/resolve-versions/versions.json");

        var result = await CreateHost(world).RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception!.Message).Contains("G54");
        await Assert.That(result.Exception.Message).Contains("sdl2-core");
        await Assert.That(result.Exception.Message).Contains("major");
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_No_ExplicitVersion_Or_ExplicitVersions_Supplied()
    {
        var world = FakeCakeWorld.CreateWindows()
            .WithManifestObject(ManifestFixture.CreateTestManifestConfig())
            .WithVersionsFile("artifacts/resolve-versions/versions.json");

        var result = await CreateHost(world).RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception!.Message).Contains("--explicit-version");
        await Assert.That(result.Exception.Message).Contains("--explicit-versions");
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_Both_ExplicitVersion_And_ExplicitVersions_Supplied()
    {
        var world = FakeCakeWorld.CreateWindows()
            .WithManifestObject(ManifestFixture.CreateTestManifestConfig())
            .WithExplicitVersion("sdl2-core=2.32.0-rc.1")
            .WithExplicitVersions("sdl2-image=2.8.0-rc.2")
            .WithVersionsFile("artifacts/resolve-versions/versions.json");

        var result = await CreateHost(world).RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception!.Message).Contains("mutually exclusive");
    }

    [Test]
    public async Task RunAsync_Should_Parse_ExplicitVersions_Comma_Separated_Form()
    {
        var world = FakeCakeWorld.CreateWindows()
            .WithManifestObject(ManifestFixture.CreateTestManifestConfig())
            .WithExplicitVersions("sdl2-core=2.32.0-test.smoke,sdl2-image=2.8.0-test.smoke")
            .WithVersionsFile("artifacts/resolve-versions/versions.json");

        var result = await CreateHost(world).RunAsync();

        await Assert.That(result.Success).IsTrue();
        var json = world.ReadAllText("artifacts/resolve-versions/versions.json");
        await Assert.That(json).Contains("\"sdl2-core\": \"2.32.0-test.smoke\"");
        await Assert.That(json).Contains("\"sdl2-image\": \"2.8.0-test.smoke\"");
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_ExplicitVersions_Comma_Form_Has_Malformed_Entry()
    {
        var world = FakeCakeWorld.CreateWindows()
            .WithManifestObject(ManifestFixture.CreateTestManifestConfig())
            .WithExplicitVersions("sdl2-core:2.32.0,sdl2-image=2.8.0")
            .WithVersionsFile("artifacts/resolve-versions/versions.json");

        var result = await CreateHost(world).RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception!.Message).Contains("could not parse operator input");
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_VersionsFile_Is_Missing()
    {
        var world = FakeCakeWorld.CreateWindows()
            .WithManifestObject(ManifestFixture.CreateTestManifestConfig())
            .WithExplicitVersion("sdl2-core=2.32.0-rc.1");

        var result = await CreateHost(world).RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception!.Message).Contains("--versions-file");
    }

    private static TargetTestHost<ResolveVersionsFromExplicitTask> CreateHost(FakeCakeWorld world)
    {
        return new TargetTestHost<ResolveVersionsFromExplicitTask>(world)
            .WithServices(services =>
            {
                services.AddData();
                services.AddSingleton<IUpstreamVersionAlignmentValidator, UpstreamVersionAlignmentValidator>();
            });
    }
}
