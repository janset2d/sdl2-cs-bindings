using Build.Repositories;
using Build.Tests.Fixtures;
using Cake.Core;
using Cake.Core.IO;

namespace Build.Tests.Unit.Repositories;

public sealed class ManifestRepositoryTests
{
    [Test]
    public async Task Load_Should_Deserialize_Valid_Manifest()
    {
        var world = FakeCakeWorldV2.CreateWindows();
        var manifestPath = world.RepoRoot.CombineWithFilePath("build/manifest.json");
        var repo = new ManifestRepository(world.CakeContext, manifestPath);

        var manifest = repo.Load();

        await Assert.That(manifest).IsNotNull();
        await Assert.That(manifest.SchemaVersion).IsEqualTo("2.1");
        await Assert.That(manifest.Runtimes.Count).IsGreaterThanOrEqualTo(1);
        await Assert.That(manifest.Runtimes[0].Rid).IsEqualTo("win-x64");
    }

    [Test]
    public async Task Load_Should_Throw_CakeException_When_File_Missing()
    {
        var world = FakeCakeWorldV2.CreateWindows();
        var manifestPath = world.RepoRoot.CombineWithFilePath("build/nonexistent.json");
        var repo = new ManifestRepository(world.CakeContext, manifestPath);

        await Assert.That(() => repo.Load()).Throws<CakeException>();
    }

    [Test]
    public async Task Load_Should_Throw_CakeException_When_File_Is_Invalid_Json()
    {
        var world = FakeCakeWorldV2.CreateWindows()
            .WithTextFile("build/bad-manifest.json", "not json {{{");
        var manifestPath = world.RepoRoot.CombineWithFilePath("build/bad-manifest.json");
        var repo = new ManifestRepository(world.CakeContext, manifestPath);

        await Assert.That(() => repo.Load()).Throws<CakeException>();
    }
}
