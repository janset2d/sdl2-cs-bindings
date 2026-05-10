using Build.Data;
using Build.Targets.ResolveVersionsFromManifest;
using Build.Tests.Fixtures;

namespace Build.Tests.Scenarios.ResolveVersionsFromManifest;

public sealed class ResolveVersionsFromManifestTaskScenarios
{
    [Test]
    public async Task RunAsync_Should_Write_Versions_With_UpstreamMajorMinor_Zero_Suffix_Per_Family()
    {
        var world = FakeCakeWorldV2.CreateWindows()
            .WithManifestObject(ManifestFixture.CreateTestManifestConfig())
            .WithSuffix("local.20260421T143022")
            .WithVersionsFile("artifacts/resolve-versions/versions.json");

        var result = await CreateHost(world).RunAsync();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Exception).IsNull();

        var json = world.ReadAllText("artifacts/resolve-versions/versions.json");
        await Assert.That(json).Contains("\"sdl2-core\": \"2.32.0-local.20260421T143022\"");
        await Assert.That(json).Contains("\"sdl2-image\": \"2.8.0-local.20260421T143022\"");
    }

    [Test]
    public async Task RunAsync_Should_Write_Only_Scoped_Families_When_Scope_Is_Subset()
    {
        var world = FakeCakeWorldV2.CreateWindows()
            .WithManifestObject(ManifestFixture.CreateTestManifestConfig())
            .WithSuffix("ci.12345")
            .WithScope("sdl2-core")
            .WithVersionsFile("artifacts/resolve-versions/versions.json");

        var result = await CreateHost(world).RunAsync();

        await Assert.That(result.Success).IsTrue();

        var json = world.ReadAllText("artifacts/resolve-versions/versions.json");
        await Assert.That(json).Contains("\"sdl2-core\": \"2.32.0-ci.12345\"");
        await Assert.That(json).DoesNotContain("sdl2-image");
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_Scoped_Family_Is_Missing_From_Manifest()
    {
        var world = FakeCakeWorldV2.CreateWindows()
            .WithManifestObject(ManifestFixture.CreateTestManifestConfig())
            .WithSuffix("ci.12345")
            .WithScope("sdl2-core", "sdl2-made-up")
            .WithVersionsFile("artifacts/resolve-versions/versions.json");

        var result = await CreateHost(world).RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception!.Message).Contains("sdl2-made-up");
        await Assert.That(result.Exception.Message).Contains("package_families");
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_Suffix_Produces_Invalid_NuGet_SemVer()
    {
        var world = FakeCakeWorldV2.CreateWindows()
            .WithManifestObject(ManifestFixture.CreateTestManifestConfig())
            .WithSuffix("bad_suffix")
            .WithVersionsFile("artifacts/resolve-versions/versions.json");

        var result = await CreateHost(world).RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception!.Message).Contains("bad_suffix");
        await Assert.That(result.Exception.Message).Contains("prerelease");
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_Suffix_Is_Missing()
    {
        var world = FakeCakeWorldV2.CreateWindows()
            .WithManifestObject(ManifestFixture.CreateTestManifestConfig())
            .WithVersionsFile("artifacts/resolve-versions/versions.json");

        var result = await CreateHost(world).RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception!.Message).Contains("--suffix");
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_Suffix_Is_Whitespace()
    {
        var world = FakeCakeWorldV2.CreateWindows()
            .WithManifestObject(ManifestFixture.CreateTestManifestConfig())
            .WithSuffix("   ")
            .WithVersionsFile("artifacts/resolve-versions/versions.json");

        var result = await CreateHost(world).RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception!.Message).Contains("--suffix");
    }

    [Test]
    public async Task RunAsync_Should_Throw_When_VersionsFile_Is_Missing()
    {
        var world = FakeCakeWorldV2.CreateWindows()
            .WithManifestObject(ManifestFixture.CreateTestManifestConfig())
            .WithSuffix("ci.12345");

        var result = await CreateHost(world).RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception!.Message).Contains("--versions-file");
    }

    private static TargetTestHostV2<ResolveVersionsFromManifestTask> CreateHost(FakeCakeWorldV2 world)
    {
        return new TargetTestHostV2<ResolveVersionsFromManifestTask>(world)
            .WithServices(services =>
            {
                services.AddData();
            });
    }
}
