using Build.Tests.Fixtures;
using Cake.Core;
using Cake.Core.IO;
using Cake.Testing;
using Microsoft.CodeAnalysis;
using NSubstitute;
using TUnit.Assertions.Enums;

namespace Build.Tests.Scenarios.GenerateBindings;

public sealed class GenerationFailureTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task RunAsync_Should_Record_Unexpected_Postprocess_Failures_But_Propagate_Cancellation(bool cancel)
    {
        var world = ImageWorld();
        Exception failure = cancel ? new OperationCanceledException("postprocess cancelled") : new NotSupportedException("unexpected postprocess failure");
        FailFirstPostprocessRead(world, failure);

        var result = await GenerationExecutionTests.CreateHost(world).RunAsync();
        var log = world.ReadAllText("artifacts/binding-generation/generation-run.md");

        if (cancel)
        {
            await Assert.That(result.Exception).IsSameReferenceAs(failure);
            await Assert.That(log.Split('\n').Where(line => line.StartsWith("Postprocess:", StringComparison.Ordinal)))
                .IsEquivalentTo(["Postprocess: platform-delta sdl2-image Compat; owner: False; handles namespace: SDL2.Image"], CollectionOrdering.Matching);
        }
        else
        {
            await Assert.That(result.Exception is CakeException cake ? cake.ExitCode : 0).IsEqualTo(3);
            await Assert.That(log).Contains("Postprocess failure: platform-delta: unexpected postprocess failure");
            await Assert.That(log).Contains("Generation exit code: 3");
            var invocations = log
                .Split('\n')
                .Where(line => line.StartsWith("Postprocess:", StringComparison.Ordinal))
                .Select(line => line.Split(';')[0]);
            await Assert.That(invocations).IsEquivalentTo(
            [
                "Postprocess: platform-delta sdl2-image Compat", "Postprocess: platform-delta sdl2-image Modern",
                "Postprocess: strip-varargs sdl2-image Compat", "Postprocess: strip-varargs sdl2-image Modern",
                "Postprocess: libraryimport sdl2-image Modern",
                "Postprocess: flags-detect sdl2-image Compat", "Postprocess: flags-detect sdl2-image Modern",
                "Postprocess: guid-substitute sdl2-image Compat", "Postprocess: guid-substitute sdl2-image Modern",
                "Postprocess: clong-dispatch sdl2-image Compat", "Postprocess: clong-dispatch sdl2-image Modern",
                "Postprocess: uniform-opaque sdl2-image Compat", "Postprocess: uniform-opaque sdl2-image Modern",
                "Postprocess: struct-layout sdl2-image Compat", "Postprocess: struct-layout sdl2-image Modern",
            ], CollectionOrdering.Matching);
        }

        await Assert.That(world.ReadAllText("spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Generated/sentinel.g.cs")).IsEqualTo("previous bytes");
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task RunAsync_Should_Record_Unexpected_ClangSharp_Failures_But_Propagate_Cancellation(bool cancel)
    {
        var world = ImageWorld();
        Exception failure = cancel ? new OperationCanceledException("process cancelled") : new NotSupportedException("unexpected process failure");
        var backends = new List<string>();
        world.CakeContext.ProcessRunner.When(runner => runner.Start(Arg.Any<FilePath>(), Arg.Any<ProcessSettings>())).Do(call =>
        {
            var args = call.Arg<ProcessSettings>().Arguments.Select(argument => CommandLineParser.SplitCommandLineIntoArguments(argument.Render(), false).Single()).ToArray();
            backends.Add(args[4]);

            if (backends.Count == 1)
            {
                throw failure;
            }
        });

        var result = await GenerationExecutionTests.CreateHost(world).RunAsync();

        if (cancel)
        {
            await Assert.That(result.Exception).IsSameReferenceAs(failure);
            await Assert.That(backends).IsEquivalentTo(["compatible-codegen"], CollectionOrdering.Matching);
        }
        else
        {
            await Assert.That(result.Exception is CakeException cake ? cake.ExitCode : 0).IsEqualTo(2);
            await Assert.That(backends).IsEquivalentTo(["compatible-codegen", "latest-codegen"], CollectionOrdering.Matching);
            var log = world.ReadAllText("artifacts/binding-generation/generation-run.md");
            await Assert.That(log).Contains("unexpected process failure");
            await Assert.That(log).Contains("Generation exit code: 2");
        }

        await Assert.That(world.ReadAllText("spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Generated/sentinel.g.cs")).IsEqualTo("previous bytes");
    }

    [Test]
    public async Task RunAsync_Should_Keep_A_Failed_Restore_Backup_Outside_Staging_Cleanup()
    {
        var world = ImageWorld();
        var system = Substitute.For<IFileSystem>();
        var publicationFailed = false;
        DirectoryPath? backup = null;
        DirectoryPath? staging = null;
        system.GetDirectory(Arg.Any<DirectoryPath>()).Returns(call =>
        {
            var path = call.Arg<DirectoryPath>();

            if (publicationFailed && path.FullPath.EndsWith("/0", StringComparison.Ordinal))
            {
                backup = path;
                throw new IOException("restore failed");
            }

            return world.FileSystem.GetDirectory(path);
        });
        system.GetFile(Arg.Any<FilePath>()).Returns(call =>
        {
            var path = call.Arg<FilePath>();

            if (path.FullPath.EndsWith("/Generated/Compat/SDL_image.g.cs", StringComparison.Ordinal))
            {
                if (path.FullPath.StartsWith("/repo/", StringComparison.Ordinal))
                {
                    publicationFailed = true;
                    throw new IOException("publication failed");
                }

                staging = path.GetDirectory().GetParent().GetParent().GetParent();
            }

            return world.FileSystem.GetFile(path);
        });
        world.CakeContext.FileSystem.Returns(system);

        var result = await GenerationExecutionTests.CreateHost(world).RunAsync();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(backup).IsNotNull();
        await Assert.That(world.FileSystem.GetFile(backup!.CombineWithFilePath("sentinel.g.cs")).Exists).IsTrue();
        await Assert.That(world.FileSystem.GetFile(backup.CombineWithFilePath("sentinel.g.cs")).GetTextContent()).IsEqualTo("previous bytes");
        await Assert.That(result.Exception!.Message).Contains(backup.FullPath);
        await Assert.That(result.Exception.ToString()).Contains("publication failed");
        await Assert.That(staging).IsNotNull();
        await Assert.That(world.FileSystem.GetDirectory(staging!).Exists).IsFalse();
        await Assert.That(backup.FullPath.StartsWith(staging!.FullPath + "/", StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    public async Task RunAsync_Should_Continue_After_Postprocess_Failure_And_Return_Three_Without_Publication()
    {
        var world = ImageWorld();
        FailFirstPostprocessRead(world, new IOException("postprocess fixture read failed"));
        var result = await GenerationExecutionTests.CreateHost(world).RunAsync();
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Exception is CakeException cake ? cake.ExitCode : 0).IsEqualTo(3);
        await Assert.That(result.Exception!.Message).IsEqualTo("Binding generation failed; see artifacts/binding-generation/generation-run.md.");
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
        await Assert.That(result.Exception!.Message).IsEqualTo("Binding generation failed; see artifacts/binding-generation/generation-run.md.");
        await Assert.That(world.ReadAllText("spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Generated/sentinel.g.cs")).IsEqualTo("previous bytes");
        await Assert.That(world.ReadAllText("spikes/binding-generators/clangsharp/src/Janset.SDL2.Gfx/Generated/sentinel.g.cs")).IsEqualTo("gfx previous bytes");
        await Assert.That(world.ProcessInvocations.Count).IsEqualTo(10);
    }

    private static void FailFirstPostprocessRead(FakeCakeWorld world, Exception failure)
    {
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
                    throw failure;
                }

                return real.Open(open.Arg<FileMode>(), open.Arg<FileAccess>(), open.Arg<FileShare>());
            });
            return file;
        });
        world.CakeContext.FileSystem.Returns(system);
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
