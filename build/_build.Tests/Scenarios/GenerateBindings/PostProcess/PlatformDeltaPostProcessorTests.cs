using Build.Targets.GenerateBindings.PostProcess;
using Build.Tests.Fixtures;
using Cake.Core.IO;
using NSubstitute;

namespace Build.Tests.Scenarios.GenerateBindings.PostProcess;

public sealed class PlatformDeltaPostProcessorTests
{
    [Test]
    public async Task ProcessAsync_Should_Copy_Neutral_Output_As_Lf_And_Remove_Platform_Duplicates()
    {
        const string source = "namespace SDL2\r\n{\r\n    internal static partial class SDLNative { }\r\n}\r\n";
        var world = PostProcessFixture.World()
            .WithTextFile("input/Generated/Compat/SDL_neutral.g.cs", source)
            .WithTextFile("input/Generated/Compat/SDL.g.cs", PostProcessFixture.Source("source"))
            .WithTextFile("input/Generated/Compat/Platforms/Android/SDL.g.cs", PostProcessFixture.Source("source"));
        var config = await PostProcessFixture.ConfigAsync(world);
        var result = await new PlatformDeltaPostProcessor(config.PlatformViews()).ProcessAsync(new GeneratedSyntaxFiles(world.CakeContext),
            world.RepoRoot.Combine("input/Generated/Compat"), world.RepoRoot.Combine("output/Generated/Compat"));
        await Assert.That(world.FileSystem.GetFile(world.RepoRoot.CombineWithFilePath("output/Generated/Compat/SDL_neutral.g.cs")).Exists).IsTrue();
        await Assert.That(world.ReadAllText("output/Generated/Compat/SDL_neutral.g.cs"))
            .IsEqualTo("namespace SDL2\n{\n    internal static partial class SDLNative { }\n}\n");
        await Assert.That(world.ReadAllText("output/Generated/Compat/Platforms/Android/SDL.g.cs")).DoesNotContain("SDL_Init");
        await Assert.That(result.RemovedDeclarations).IsEqualTo(1);
    }

    [Test]
    public async Task ProcessAsync_Should_Copy_Neutral_Files_Without_Enumerating_An_Absent_Platform_Directory()
    {
        var world = PostProcessFixture.World().WithTextFile("input/SDL.g.cs", PostProcessFixture.Source("source"));
        var config = await PostProcessFixture.ConfigAsync(world);
        var boundary = Substitute.For<IFileSystem>();
        var missing = Substitute.For<IDirectory>();
        missing.Exists.Returns(false);
        missing.GetFiles("*.g.cs", SearchScope.Recursive).Returns(_ => throw new DirectoryNotFoundException("Platforms"));
        boundary.GetFile(Arg.Any<FilePath>()).Returns(call => world.FileSystem.GetFile(call.Arg<FilePath>()));
        boundary.GetDirectory(Arg.Any<DirectoryPath>()).Returns(call =>
            call.Arg<DirectoryPath>().GetDirectoryName() == "Platforms" ? missing : world.FileSystem.GetDirectory(call.Arg<DirectoryPath>()));
        world.CakeContext.FileSystem.Returns(boundary);
        await new PlatformDeltaPostProcessor(config.PlatformViews()).ProcessAsync(new GeneratedSyntaxFiles(world.CakeContext),
            world.RepoRoot.Combine("input"), world.RepoRoot.Combine("output"));
        await Assert.That(world.FileSystem.GetFile(world.RepoRoot.CombineWithFilePath("output/SDL.g.cs")).Exists).IsTrue();
        await Assert.That(world.ReadAllText("output/SDL.g.cs")).IsEqualTo(PostProcessFixture.Source("source"));
    }
}
