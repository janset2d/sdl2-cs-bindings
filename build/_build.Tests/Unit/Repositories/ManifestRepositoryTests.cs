using Build.Repositories;
using Build.Tests.Fixtures;
using Cake.Core;
using Cake.Core.IO;
using NSubstitute;

namespace Build.Tests.Unit.Repositories;

/// <summary>
/// Mock-based unit coverage for <see cref="ManifestRepository"/> — constructor argument
/// validation. Load behavior (JSON deserialization, file-not-found / invalid-JSON failure
/// surface) is covered end-to-end by <see cref="ManifestRepositoryRoundTripTests"/> against
/// FakeFileSystem; mocking the <see cref="ICakeContext.ToJson{T}"/> extension chain isn't
/// productive here and the sociable tests already verify the contract bytewise.
/// </summary>
public sealed class ManifestRepositoryUnitTests
{
    [Test]
    public async Task Constructor_Should_Throw_When_CakeContext_Is_Null()
    {
        var path = new FilePath("/repo/build/manifest.json");
        await Assert.That(() => new ManifestRepository(null!, path)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Constructor_Should_Throw_When_ManifestPath_Is_Null()
    {
        var ctx = Substitute.For<ICakeContext>();
        await Assert.That(() => new ManifestRepository(ctx, null!)).Throws<ArgumentNullException>();
    }
}

/// <summary>
/// Sociable round-trip coverage for <see cref="ManifestRepository"/>. Pairs with
/// <see cref="ManifestRepositoryUnitTests"/> per the repository-cohort rule.
/// </summary>
public sealed class ManifestRepositoryRoundTripTests
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
