using Build.Tests.Fixtures;
using Cake.Core.IO;

namespace Build.Tests.Unit.Fixtures;

public sealed class FakeCakeWorldV2Tests
{
    [Test]
    public async Task Create_Should_Set_Up_Windows_Fake_Environment_By_Default()
    {
        var world = FakeCakeWorldV2.Create();

        await Assert.That(world.Environment.Platform.Family)
            .IsEqualTo(Cake.Core.PlatformFamily.Windows);
        await Assert.That(world.FileSystem).IsNotNull();
        await Assert.That(world.Log).IsNotNull();
        await Assert.That(world.CakeContext).IsNotNull();
    }

    [Test]
    public async Task Create_Should_Set_Up_Unix_Fake_Environment_When_Specified()
    {
        var world = FakeCakeWorldV2.Create(FakeRepoPlatformV2.Unix);

        await Assert.That(world.Environment.Platform.Family)
            .IsEqualTo(Cake.Core.PlatformFamily.Linux);
    }

    [Test]
    public async Task WithTextFile_Should_Create_File_In_Fake_Filesystem()
    {
        var world = FakeCakeWorldV2.Create();

        world.WithTextFile("test/data.json", "{\"key\":42}");

        var fullPath = world.RepoRoot.CombineWithFilePath("test/data.json");
        var exists = world.FileSystem.GetFile(fullPath).Exists;
        await Assert.That(exists).IsTrue();
    }

    [Test]
    public async Task ReadAllText_Should_Return_File_Content()
    {
        var world = FakeCakeWorldV2.Create();
        world.WithTextFile("readme.md", "hello");

        var content = world.ReadAllText("readme.md");

        await Assert.That(content).IsEqualTo("hello");
    }

    [Test]
    public async Task FileExists_Should_Return_True_For_Existing_File()
    {
        var world = FakeCakeWorldV2.Create();
        world.WithTextFile("exists.txt", "");

        await Assert.That(world.FileExists("exists.txt")).IsTrue();
    }

    [Test]
    public async Task FileExists_Should_Return_False_For_Missing_File()
    {
        var world = FakeCakeWorldV2.Create();

        await Assert.That(world.FileExists("missing.txt")).IsFalse();
    }
}
