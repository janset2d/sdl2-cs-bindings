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
        var source = PostProcessFixture.Source("neutralCrLf");
        var world = PostProcessFixture.World()
            .WithTextFile("input/Generated/Compat/SDL_neutral.g.cs", source)
            .WithTextFile("input/Generated/Compat/SDL.g.cs", PostProcessFixture.Source("source"))
            .WithTextFile("input/Generated/Compat/Platforms/Android/SDL.g.cs", PostProcessFixture.Source("source"));
        var config = await PostProcessFixture.ConfigAsync(world);
        var result = await new PlatformDeltaPostProcessor(config.PlatformViews()).ProcessAsync(new GeneratedSyntaxFiles(world.CakeContext),
            world.RepoRoot.Combine("input/Generated/Compat"), world.RepoRoot.Combine("output/Generated/Compat"));
        await Assert.That(world.FileSystem.GetFile(world.RepoRoot.CombineWithFilePath("output/Generated/Compat/SDL_neutral.g.cs")).Exists).IsTrue();
        await Assert.That(world.ReadAllText("output/Generated/Compat/SDL_neutral.g.cs"))
            .IsEqualTo(PostProcessFixture.Source("neutralLf"));
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

    [Test]
    public async Task ProcessAsync_Should_Preserve_Empty_Nonmatching_Trivia_And_Repeated_Configured_Output()
    {
        var nonmatching = PostProcessFixture.Source("nonmatching");
        var world = PostProcessFixture.World()
            .WithTextFile("input/Empty.g.cs", "")
            .WithTextFile("input/Other.g.cs", nonmatching)
            .WithTextFile("input/Platforms/WinRT/SDL.g.cs", PostProcessFixture.Source("platformTrivia"));
        var config = await PostProcessFixture.ConfigAsync(world);
        var files = new GeneratedSyntaxFiles(world.CakeContext);
        var processor = new PlatformDeltaPostProcessor(config.PlatformViews());
        var input = world.RepoRoot.Combine("input");
        var output = world.RepoRoot.Combine("output");
        await processor.ProcessAsync(files, input, output);
        await Assert.That(world.ReadAllText("output/Empty.g.cs")).IsEqualTo("");
        await Assert.That(world.ReadAllText("output/Other.g.cs")).IsEqualTo(nonmatching);
        var platform = world.ReadAllText("output/Platforms/WinRT/SDL.g.cs");
        await Assert.That(platform).Contains("// keep the platform comment");
        await Assert.That(platform).Contains("[SupportedOSPlatform(\"windows10.0.10240.0\")]");
        await Assert.That(platform).Contains("public static extern int SDL_PlatformOnly(); // keep the tail");
        await processor.ProcessAsync(files, output, output);
        await Assert.That(world.ReadAllText("output/Platforms/WinRT/SDL.g.cs")).IsEqualTo(platform);
    }
}
