using Build.Tests.Fixtures;
using Cake.Core;
using Cake.Core.IO;
using Microsoft.CodeAnalysis;
using NSubstitute;

namespace Build.Tests.Scenarios.GenerateBindings;

public sealed class GenerationFailureTests
{
    [Test]
    public async Task RunAsync_Should_Continue_After_Postprocess_Failure_And_Return_Three_Without_Publication()
    {
        var world = ImageWorld();
        var system = Substitute.For<IFileSystem>();
        system.GetDirectory(Arg.Any<DirectoryPath>()).Returns(call => world.FileSystem.GetDirectory(call.Arg<DirectoryPath>()));
        var injected = false;
        system.GetFile(Arg.Any<FilePath>()).Returns(call =>
        {
            var real = world.FileSystem.GetFile(call.Arg<FilePath>());

            if (!real.Exists || !real.Path.FullPath.EndsWith("/Compat/SDL_image.g.cs", StringComparison.Ordinal))
            {
                return real;
            }

            var file = Substitute.For<IFile>();
            file.Path.Returns(real.Path);
            file.Exists.Returns(true);
            file.Length.Returns(real.Length);
            file.Open(Arg.Any<FileMode>(), Arg.Any<FileAccess>(), Arg.Any<FileShare>()).Returns(open =>
            {
                if (!injected)
                {
                    injected = true;
                    throw new IOException("postprocess fixture read failed");
                }

                return real.Open(open.Arg<FileMode>(), open.Arg<FileAccess>(), open.Arg<FileShare>());
            });
            return file;
        });
        world.CakeContext.FileSystem.Returns(system);
        var result = await GenerationExecutionTests.CreateHost(world).RunAsync();
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception is CakeException cake ? cake.ExitCode : 0).IsEqualTo(3);
        await Assert.That(world.ReadAllText("spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Generated/sentinel.g.cs")).IsEqualTo("previous bytes");
        var log = world.ReadAllText("artifacts/binding-generation/generation-run.md");
        await Assert.That(log).Contains("Postprocess failure: platform-delta: postprocess fixture read failed");
        await Assert.That(log).Contains("Postprocess: struct-layout sdl2-image Modern");
        await Assert.That(world.ProcessInvocations.Count).IsEqualTo(2);
    }

    [Test]
    public async Task RunAsync_Should_Keep_Earlier_Families_Unpublished_When_A_Later_Family_Fails()
    {
        var world = ImageWorld().WithScope("sdl2-image", "sdl2-gfx");

        foreach (var header in new[] { "SDL2_framerate.h", "SDL2_gfxPrimitives.h", "SDL2_imageFilter.h", "SDL2_rotozoom.h" })
        {
            world.WithTextFile("vcpkg_installed/x64-linux-hybrid/include/SDL2/" + header, "");
        }

        world.WithTextFile("spikes/binding-generators/clangsharp/src/Janset.SDL2.Gfx/Generated/sentinel.g.cs", "gfx previous bytes");
        world.CakeContext.ProcessRunner.When(runner => runner.Start(Arg.Any<FilePath>(), Arg.Any<ProcessSettings>())).Do(call =>
        {
            var settings = call.Arg<ProcessSettings>();
            var args = settings.Arguments.Select(argument => CommandLineParser.SplitCommandLineIntoArguments(argument.Render(), false).Single()).ToArray();
            var output = args[Array.IndexOf(args, "--output") + 1];

            if (output.EndsWith("/Modern/SDL2_rotozoom.g.cs", StringComparison.Ordinal))
            {
                world.WithProcessResult("dotnet", 1, GenerationScenarioData.UnknownWarning);
            }
        });
        var result = await GenerationExecutionTests.CreateHost(world).RunAsync();
        await Assert.That(result.Exception is CakeException cake ? cake.ExitCode : 0).IsEqualTo(2);
        await Assert.That(world.ReadAllText("spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Generated/sentinel.g.cs")).IsEqualTo("previous bytes");
        await Assert.That(world.ReadAllText("spikes/binding-generators/clangsharp/src/Janset.SDL2.Gfx/Generated/sentinel.g.cs")).IsEqualTo("gfx previous bytes");
        await Assert.That(world.ProcessInvocations.Count).IsEqualTo(10);
    }

    private static FakeCakeWorld ImageWorld()
    {
        var world = FakeCakeWorld.CreateLinux()
            .WithManifestObject(ManifestFixture.RealManifest)
            .WithGeneration()
            .WithScope("sdl2-image")
            .WithTextFile("vcpkg_installed/x64-linux-hybrid/include/SDL2/SDL_image.h", "")
            .WithTextFile("spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Generated/sentinel.g.cs", "previous bytes")
            .WithToolPath("/usr/bin/dotnet")
            .WithProcessResult("dotnet", 0, "");
        world.CakeContext.ProcessRunner.When(runner => runner.Start(Arg.Any<FilePath>(), Arg.Any<ProcessSettings>())).Do(call =>
        {
            var settings = call.Arg<ProcessSettings>();
            var args = settings.Arguments.Select(argument => CommandLineParser.SplitCommandLineIntoArguments(argument.Render(), false).Single()).ToArray();
            world.WithTextFile(args[Array.IndexOf(args, "--output") + 1], FixtureLoader.Load("BindingGeneration/Generation/image-neutral.cs.txt"));
        });
        return world;
    }
}
