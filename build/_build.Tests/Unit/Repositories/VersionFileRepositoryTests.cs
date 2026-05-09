using Build.Repositories;
using Build.Tests.Fixtures;
using Build.Versioning;
using Cake.Core;
using Cake.Core.IO;
using NSubstitute;
using NuGet.Versioning;

namespace Build.Tests.Unit.Repositories;

/// <summary>
/// Mock-based unit coverage for <see cref="VersionFileRepository"/> — constructor + argument
/// validation. End-to-end Load/SaveAsync round-trip against FakeFileSystem lives in
/// <see cref="VersionFileRepositoryRoundTripTests"/>.
/// </summary>
public sealed class VersionFileRepositoryUnitTests
{
    private static readonly PackageFamilyId Sdl2Core = new("sdl2-core");

    [Test]
    public async Task Constructor_Should_Throw_When_CakeContext_Is_Null()
    {
        await Assert.That(() => new VersionFileRepository(null!)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Load_Should_Throw_When_Path_Is_Null()
    {
        var ctx = Substitute.For<ICakeContext>();
        var repo = new VersionFileRepository(ctx);
        await Assert.That(() => repo.Load(null!)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task SaveAsync_Should_Throw_When_Path_Is_Null()
    {
        var ctx = Substitute.For<ICakeContext>();
        var repo = new VersionFileRepository(ctx);
        var versions = new PackageFamilyVersionSet([new PackageFamilyVersion(Sdl2Core, NuGetVersion.Parse("2.32.0"))]);
        await Assert.That(() => repo.SaveAsync(null!, versions)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task SaveAsync_Should_Throw_When_Versions_Are_Null()
    {
        var ctx = Substitute.For<ICakeContext>();
        var repo = new VersionFileRepository(ctx);
        await Assert.That(() => repo.SaveAsync(new FilePath("artifacts/versions.json"), null!)).Throws<ArgumentNullException>();
    }
}

/// <summary>
/// Sociable round-trip coverage for <see cref="VersionFileRepository"/>. Pairs with
/// <see cref="VersionFileRepositoryUnitTests"/> per the repository-cohort rule.
/// </summary>
public sealed class VersionFileRepositoryRoundTripTests
{
    private static readonly PackageFamilyId Sdl2Core = new("sdl2-core");
    private static readonly PackageFamilyId Sdl2Image = new("sdl2-image");

    [Test]
    public async Task Load_Should_Deserialize_Valid_Versions_File()
    {
        var content = FixtureLoader.Load("Versions/versions-valid.json");
        var world = FakeCakeWorldV2.CreateWindows().WithTextFile("artifacts/versions.json", content);
        var path = world.RepoRoot.CombineWithFilePath("artifacts/versions.json");
        var repo = new VersionFileRepository(world.CakeContext);
        var set = repo.Load(path);

        await Assert.That(set.Count).IsEqualTo(2);
        await Assert.That(set.RequireVersion(Sdl2Core)).IsEqualTo(NuGetVersion.Parse("2.32.0"));
        await Assert.That(set.RequireVersion(Sdl2Image)).IsEqualTo(NuGetVersion.Parse("2.8.0"));
    }

    [Test]
    public async Task Load_Should_Return_Empty_Set_For_Empty_Json_Object()
    {
        var content = FixtureLoader.Load("Versions/versions-empty.json");
        var world = FakeCakeWorldV2.CreateWindows().WithTextFile("artifacts/versions.json", content);
        var path = world.RepoRoot.CombineWithFilePath("artifacts/versions.json");
        var repo = new VersionFileRepository(world.CakeContext);
        var set = repo.Load(path);

        await Assert.That(set.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Load_Should_Throw_CakeException_When_File_Missing()
    {
        var world = FakeCakeWorldV2.CreateWindows();
        var path = world.RepoRoot.CombineWithFilePath("missing.json");
        var repo = new VersionFileRepository(world.CakeContext);

        await Assert.That(() => repo.Load(path)).Throws<CakeException>().And.HasMessageContaining("does not exist");
    }

    [Test]
    public async Task SaveAsync_Should_Write_And_Roundtrip()
    {
        var world = FakeCakeWorldV2.CreateWindows();
        var path = world.RepoRoot.CombineWithFilePath("artifacts/versions.json");
        var original = new PackageFamilyVersionSet(
        [
            new PackageFamilyVersion(Sdl2Core, NuGetVersion.Parse("2.32.0")),
            new PackageFamilyVersion(Sdl2Image, NuGetVersion.Parse("2.8.0")),
        ]);

        var repo = new VersionFileRepository(world.CakeContext);
        await repo.SaveAsync(path, original);

        await Assert.That(world.FileExists("artifacts/versions.json")).IsTrue();

        var roundtripped = repo.Load(path);
        await Assert.That(roundtripped).IsEqualTo(original);
    }

    [Test]
    public async Task SaveAsync_Should_Overwrite_Existing_File()
    {
        var content = FixtureLoader.Load("Versions/versions-single-family.json");
        var world = FakeCakeWorldV2.CreateWindows().WithTextFile("artifacts/versions.json", content);
        var path = world.RepoRoot.CombineWithFilePath("artifacts/versions.json");
        var updated = new PackageFamilyVersionSet(
        [
            new PackageFamilyVersion(Sdl2Core, NuGetVersion.Parse("2.32.0")),
        ]);

        var repo = new VersionFileRepository(world.CakeContext);
        await repo.SaveAsync(path, updated);

        var loaded = repo.Load(path);
        await Assert.That(loaded.RequireVersion(Sdl2Core)).IsEqualTo(NuGetVersion.Parse("2.32.0"));
    }

    [Test]
    public async Task SaveAsync_Should_Create_Parent_Directory_When_Missing()
    {
        var world = FakeCakeWorldV2.CreateWindows();
        var path = world.RepoRoot.CombineWithFilePath("deep/nested/versions.json");
        var versions = new PackageFamilyVersionSet(
        [
            new PackageFamilyVersion(Sdl2Core, NuGetVersion.Parse("2.32.0")),
        ]);

        var repo = new VersionFileRepository(world.CakeContext);
        await repo.SaveAsync(path, versions);

        await Assert.That(world.FileExists("deep/nested/versions.json")).IsTrue();
        var loaded = repo.Load(path);
        await Assert.That(loaded.RequireVersion(Sdl2Core)).IsEqualTo(NuGetVersion.Parse("2.32.0"));
    }
}
