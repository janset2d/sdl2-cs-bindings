using Build.Tests.Fixtures;
using Cake.Core.IO;
using Microsoft.CodeAnalysis;
using NSubstitute;
using TUnit.Assertions.Enums;

namespace Build.Tests.Scenarios.GenerateBindings;

public sealed class PlatformGenerationTests
{
    [Test]
    public async Task RunAsync_Should_Pass_Raw_Neutral_Exclusions_In_Declaration_Order_To_Every_View()
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
                        Headers = [.. library.BindingGeneration.HeaderSet.Headers.Where(header => header.Name is "SDL_main.h" or "SDL_system.h")],
                    },
                    RequiredFunctions = [], RequiredConstants = [], Dynapi = null,
                },
            } : library)],
        };
        var world = FakeCakeWorld.CreateLinux()
            .WithManifestObject(manifest)
            .WithGeneration(platformHeaderShims: true)
            .WithScope("sdl2-core")
            .WithTextFile("vcpkg_installed/x64-linux-hybrid/include/SDL2/SDL_main.h", "")
            .WithTextFile("vcpkg_installed/x64-linux-hybrid/include/SDL2/SDL_system.h", "")
            .WithTextFile("vcpkg_installed/x64-linux-hybrid/include/SDL2/SDL.h", "")
            .WithToolPath("/usr/bin/dotnet")
            .WithProcessResult("dotnet", 0, "");
        var platformArguments = new List<string[]>();
        world.CakeContext.ProcessRunner.When(runner => runner.Start(Arg.Any<FilePath>(), Arg.Any<ProcessSettings>())).Do(call =>
        {
            var settings = call.Arg<ProcessSettings>();
            var args = settings.Arguments.Select(argument => CommandLineParser.SplitCommandLineIntoArguments(argument.Render(), false).Single()).ToArray();
            var output = args[Array.IndexOf(args, "--output") + 1];

            if (args.Contains("--define-macro", StringComparer.Ordinal))
            {
                platformArguments.Add(args);
                world.WithTextFile(output, "");
            }
            else
            {
                var fixture = output.Contains("/Compat/", StringComparison.Ordinal) ? "neutral-main-compat.txt" : "neutral-main-modern.txt";

                if (output.EndsWith("SDL_system.g.cs", StringComparison.Ordinal))
                {
                    fixture = "neutral-system.txt";
                }

                world.WithTextFile(output, FixtureLoader.Load("BindingGeneration/Generation/" + fixture));
            }
        });
        GeneratedStampWorld.SeedInputs(world);
        var result = await GenerationExecutionTests.CreateHost(world).RunAsync();
        await Assert.That(result.Exception).IsNull();
        await Assert.That(platformArguments.Count).IsEqualTo(28);

        foreach (var args in platformArguments)
        {
            var output = args[Array.IndexOf(args, "--output") + 1];
            string[] expected = output.Contains("/Compat/", StringComparison.Ordinal)
                ? ["SDL_main_func", "SDL_main", "SDL_SetMainReady"] : ["SDL_main", "SDL_SetMainReady"];

            if (output.EndsWith("SDL_system.g.cs", StringComparison.Ordinal))
            {
                expected = ["SDL_IsTablet", "SDL_OnApplicationWillTerminate", "SDL_OnApplicationDidReceiveMemoryWarning", "SDL_OnApplicationWillResignActive",
                    "SDL_OnApplicationDidEnterBackground", "SDL_OnApplicationWillEnterForeground", "SDL_OnApplicationDidBecomeActive"];
            }

            await Assert.That(args.Skip(Array.IndexOf(args, "--exclude") + 1)).IsEquivalentTo(expected, CollectionOrdering.Matching);
        }

        await Assert.That(world.ProcessInvocations.Count).IsEqualTo(32);
    }
}
