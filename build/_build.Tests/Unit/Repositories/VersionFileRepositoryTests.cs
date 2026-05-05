using Build.Repositories;
using Build.Tests.Fixtures;
using Build.Versioning;
using Cake.Core;
using Cake.Core.IO;
using NuGet.Versioning;

namespace Build.Tests.Unit.Repositories;

public sealed class VersionFileRepositoryTests
{
    private static readonly PackageFamilyId Sdl2Core = new("sdl2-core");
    private static readonly PackageFamilyId Sdl2Image = new("sdl2-image");

    [Test]
    public async Task Load_Should_Deserialize_Valid_Versions_File()
    {
        var world = FakeCakeWorldV2.CreateWindows()
            .WithTextFile("artifacts/versions.json", """
            {
              "sdl2-core": "2.32.0",
              "sdl2-image": "2.8.0"
            }
            """);
        var path = world.RepoRoot.CombineWithFilePath("artifacts/versions.json");
        var repo = new VersionFileRepository(world.CakeContext, path);
        var set = repo.Load();

        await Assert.That(set.Count).IsEqualTo(2);
        await Assert.That(set.RequireVersion(Sdl2Core)).IsEqualTo(NuGetVersion.Parse("2.32.0"));
        await Assert.That(set.RequireVersion(Sdl2Image)).IsEqualTo(NuGetVersion.Parse("2.8.0"));
    }

    [Test]
    public async Task Load_Should_Return_Empty_Set_For_Empty_Json_Object()
    {
        var world = FakeCakeWorldV2.CreateWindows()
            .WithTextFile("artifacts/versions.json", "{}");
        var path = world.RepoRoot.CombineWithFilePath("artifacts/versions.json");
        var repo = new VersionFileRepository(world.CakeContext, path);
        var set = repo.Load();

        await Assert.That(set.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Load_Should_Throw_CakeException_When_File_Missing()
    {
        var world = FakeCakeWorldV2.CreateWindows();
        var path = world.RepoRoot.CombineWithFilePath("missing.json");
        var repo = new VersionFileRepository(world.CakeContext, path);

        await Assert.That(() => repo.Load()).Throws<CakeException>();
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

        var repo = new VersionFileRepository(world.CakeContext, path);
        await repo.SaveAsync(original);

        await Assert.That(world.FileExists("artifacts/versions.json")).IsTrue();

        var roundtripped = repo.Load();
        await Assert.That(roundtripped).IsEqualTo(original);
    }

    [Test]
    public async Task SaveAsync_Should_Overwrite_Existing_File()
    {
        var world = FakeCakeWorldV2.CreateWindows()
            .WithTextFile("artifacts/versions.json", """{"sdl2-core":"1.0.0"}""");
        var path = world.RepoRoot.CombineWithFilePath("artifacts/versions.json");
        var updated = new PackageFamilyVersionSet(
        [
            new PackageFamilyVersion(Sdl2Core, NuGetVersion.Parse("2.32.0")),
        ]);

        var repo = new VersionFileRepository(world.CakeContext, path);
        await repo.SaveAsync(updated);

        var loaded = repo.Load();
        await Assert.That(loaded.RequireVersion(Sdl2Core)).IsEqualTo(NuGetVersion.Parse("2.32.0"));
    }
}
