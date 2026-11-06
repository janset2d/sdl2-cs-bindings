using Build.Data.Manifest;
using Build.Tests.Fixtures;
using Cake.Core;

namespace Build.Tests.Unit.Data.Manifest;

/// <summary>
/// Sociable round-trip coverage for <see cref="VcpkgManifestRepository"/>. Pairs with
/// <see cref="VcpkgManifestRepositoryUnitTests"/> per the repository-cohort rule.
/// Real <c>ICakeContext</c> + <see cref="Cake.Testing.FakeFileSystem"/> + real System.Text.Json
/// deserialize — no reader mock, no NSubstitute round-trip stand-in.
/// </summary>
public sealed class VcpkgManifestRepositoryRoundTripTests
{
    [Test]
    public async Task Load_Should_Return_Parsed_Manifest_When_File_Exists()
    {
        var world = FakeCakeWorld.CreateWindows()
            .WithTextFile("vcpkg.json", FixtureLoader.Load("Vcpkg/vcpkg-valid.json"));

        var path = world.RepoRoot.CombineWithFilePath("vcpkg.json");
        var repository = new VcpkgManifestRepository(world.CakeContext);

        var loaded = repository.Load(path);

        await Assert.That(loaded.Overrides).IsNotNull();
        await Assert.That(loaded.Overrides!.Count).IsEqualTo(2);
        await Assert.That(loaded.Overrides[0].Name).IsEqualTo("sdl2");
        await Assert.That(loaded.Overrides[0].Version).IsEqualTo("2.32.10");
        await Assert.That(loaded.Overrides[0].PortVersion).IsEqualTo(0);
        await Assert.That(loaded.Overrides[1].Name).IsEqualTo("sdl2-image");
    }

    [Test]
    public async Task Load_Should_Throw_CakeException_When_File_Is_Missing()
    {
        var world = FakeCakeWorld.CreateWindows();
        var path = world.RepoRoot.CombineWithFilePath("vcpkg.json");
        var repository = new VcpkgManifestRepository(world.CakeContext);

        var ex = await Assert.That(() => repository.Load(path)).Throws<CakeException>();

        await Assert.That(ex!.Message).Contains("vcpkg manifest");
        await Assert.That(ex.Message).Contains("does not exist");
    }

    [Test]
    public async Task Load_Should_Throw_CakeException_When_Json_Is_Invalid()
    {
        var world = FakeCakeWorld.CreateWindows()
            .WithTextFile("vcpkg.json", "{ this is not valid json");

        var path = world.RepoRoot.CombineWithFilePath("vcpkg.json");
        var repository = new VcpkgManifestRepository(world.CakeContext);

        var ex = await Assert.That(() => repository.Load(path)).Throws<CakeException>();

        await Assert.That(ex!.Message).Contains("vcpkg manifest");
        await Assert.That(ex.Message).Contains("invalid JSON");
    }

    [Test]
    public async Task Load_Should_Throw_CakeException_When_Json_Deserializes_To_Null()
    {
        var world = FakeCakeWorld.CreateWindows()
            .WithTextFile("vcpkg.json", "null");

        var path = world.RepoRoot.CombineWithFilePath("vcpkg.json");
        var repository = new VcpkgManifestRepository(world.CakeContext);

        var ex = await Assert.That(() => repository.Load(path)).Throws<CakeException>();

        await Assert.That(ex!.Message).Contains("deserialized to null");
    }
}
