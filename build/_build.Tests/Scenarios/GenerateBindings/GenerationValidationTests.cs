using Build.Tests.Fixtures;
using Cake.Core;
using Cake.Core.IO;
using Microsoft.CodeAnalysis;
using NSubstitute;

namespace Build.Tests.Scenarios.GenerateBindings;

public sealed class GenerationValidationTests
{
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task RunAsync_Should_Emit_Required_Surface_And_Validate_Dynapi_Before_Publication(bool extraImport)
    {
        var manifest = ManifestFixture.RealManifest;
        manifest = manifest with
        {
            LibraryManifests = [.. manifest.LibraryManifests.Select(library => library.Name == "SDL2" ? library with
            {
                BindingGeneration = library.BindingGeneration with
                {
                    HeaderSet = library.BindingGeneration.HeaderSet! with
                    {
                        Headers = [.. library.BindingGeneration.HeaderSet.Headers.Where(header => header.Name == "SDL_main.h")],
                    },
                    PlatformSensitiveHeaders = [],
                },
            } : library)],
        };
        var root = "spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated";
        var world = FakeCakeWorld.CreateLinux()
            .WithManifestObject(manifest)
            .WithGeneration()
            .WithScope("sdl2-core")
            .WithTextFile("vcpkg_installed/x64-linux-hybrid/include/SDL2/SDL_main.h", "")
            .WithTextFile("vcpkg_installed/x64-linux-hybrid/include/SDL2/SDL.h", FixtureLoader.Load("BindingGeneration/Generation/coherence-header.txt"))
            .WithTextFile("vcpkg_installed/vcpkg/blds/sdl2/src/version/src/dynapi/SDL2.exports", FixtureLoader.Load("BindingGeneration/Oracle/coherence.exports"))
            .WithTextFile(root + "/sentinel.g.cs", "previous bytes")
            .WithToolPath("/usr/bin/dotnet")
            .WithProcessResult("dotnet", 0, "");
        world.CakeContext.ProcessRunner.When(runner => runner.Start(Arg.Any<FilePath>(), Arg.Any<ProcessSettings>())).Do(call =>
        {
            var settings = call.Arg<ProcessSettings>();
            var args = settings.Arguments
                .Select(argument => CommandLineParser.SplitCommandLineIntoArguments(argument.Render(), false).Single())
                .ToArray();
            world.WithTextFile(args[Array.IndexOf(args, "--output") + 1], extraImport ? FixtureLoader.Load("BindingGeneration/Oracle/coherence.cs.txt") : "");
        });
        var result = await GenerationExecutionTests.CreateHost(world).RunAsync();

        if (extraImport)
        {
            await Assert.That(result.Success).IsFalse();
            await Assert.That(result.Exception).IsTypeOf<CakeException>();
            await Assert.That(world.ReadAllText(root + "/sentinel.g.cs")).IsEqualTo("previous bytes");
            await Assert.That(world.FileExists(root + "/Compat/SDL_required.g.cs")).IsFalse();
        }
        else
        {
            await Assert.That(result.Exception).IsNull();
            await Assert.That(world.ReadAllText(root + "/Compat/SDL_required.g.cs"))
                .Contains("public static extern int SDL_Init([NativeTypeName(\"Uint32\")] uint flags);");
            await Assert.That(world.ReadAllText(root + "/Modern/SDL_required.g.cs")).Contains("[LibraryImport(\"SDL2\"");
            await Assert.That(world.ReadAllText(root + "/Public/SDL.g.cs")).Contains("SDL_Init");
        }

        await Assert.That(world.ReadAllText("artifacts/binding-generation/generation-run.md"))
            .Contains(extraImport ? "Validator dynapi-coherence: sdl2-core; errors: 1; warnings: 0" : "Validator dynapi-coherence: sdl2-core; errors: 0; warnings: 0");
        await Assert.That(world.ProcessInvocations.Count).IsEqualTo(2);
    }
}
