using Build.Data.Manifest;
using Build.Tests.Fixtures;
using Cake.Core;

namespace Build.Tests.Unit.Data.Manifest;
/// <summary>
/// Sociable round-trip coverage for <see cref="ManifestRepository"/>. Pairs with
/// <see cref="ManifestRepositoryUnitTests"/> per the repository-cohort rule.
/// </summary>
public sealed class ManifestRepositoryRoundTripTests
{
    [Test]
    public async Task Load_Should_Deserialize_Valid_Manifest()
    {
        var world = FakeCakeWorld.CreateWindows();
        var manifestPath = world.RepoRoot.CombineWithFilePath("build/manifest.json");
        var repo = new ManifestRepository(world.CakeContext, manifestPath);

        var manifest = repo.Load();

        await Assert.That(manifest).IsNotNull();
        await Assert.That(manifest.SchemaVersion).IsEqualTo("2.2");
        await Assert.That(manifest.Runtimes.Count).IsGreaterThanOrEqualTo(1);
        await Assert.That(manifest.Runtimes[0].Rid).IsEqualTo("win-x64");
    }

    [Test]
    public async Task Load_Should_Throw_CakeException_When_File_Missing()
    {
        var world = FakeCakeWorld.CreateWindows();
        var manifestPath = world.RepoRoot.CombineWithFilePath("build/nonexistent.json");
        var repo = new ManifestRepository(world.CakeContext, manifestPath);

        await Assert.That(() => repo.Load()).Throws<CakeException>();
    }

    [Test]
    public async Task Load_Should_Throw_CakeException_When_File_Is_Invalid_Json()
    {
        var world = FakeCakeWorld.CreateWindows()
            .WithTextFile("build/bad-manifest.json", "not json {{{");
        var manifestPath = world.RepoRoot.CombineWithFilePath("build/bad-manifest.json");
        var repo = new ManifestRepository(world.CakeContext, manifestPath);

        await Assert.That(() => repo.Load()).Throws<CakeException>();
    }
}
