using System.Globalization;
using Build.Data;
using Build.Targets.GenerateBindings;
using Build.Tests.Fixtures;
using Build.Validation;
using Cake.Core;
using Cake.Core.IO;
using Microsoft.CodeAnalysis;
using NSubstitute;
using TUnit.Assertions.Enums;

namespace Build.Tests.Scenarios.GenerateBindings;

public sealed class GenerationExecutionTests
{
    [Test]
    [Arguments(1, true, false, GenerationScenarioData.MacroWarning, 0)]
    [Arguments(1, true, true, GenerationScenarioData.MacroWarning, 0)]
    [Arguments(1, true, false, GenerationScenarioData.UnknownWarning, 2)]
    [Arguments(1, true, false, GenerationScenarioData.MacroWarning + "\n" + GenerationScenarioData.UnknownWarning, 2)]
    [Arguments(1, false, false, GenerationScenarioData.MacroWarning, 2)]
    [Arguments(0, false, false, "", 4)]
    [Arguments(0, true, true, "", 0)]
    [Arguments(0, true, false, "", 0)]
    public async Task RunAsync_Should_Classify_Exits_And_Output_Without_Publishing_A_Failed_Run(
        int toolExit, bool exists, bool empty, string diagnostics, int expectedExit)
    {
        var raw = FixtureLoader.Load("BindingGeneration/Generation/image-neutral.cs.txt");
        var root = "spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Generated";
        var world = FakeCakeWorld
            .CreateLinux()
            .WithManifestObject(ManifestFixture.RealManifest)
            .WithGeneration()
            .WithScope("sdl2-image")
            .WithTextFile("vcpkg_installed/x64-linux-hybrid/include/SDL2/SDL_image.h", "header")
            .WithTextFile(root + "/sentinel.g.cs", "previous bytes")
            .WithToolPath("/usr/bin/dotnet")
            .WithProcessResult("dotnet", toolExit, diagnostics);
        world.CakeContext.ProcessRunner.When(runner => runner.Start(Arg.Any<FilePath>(), Arg.Any<ProcessSettings>())).Do(call =>
        {
            var args = Tokens(call.Arg<ProcessSettings>());

            if (exists)
            {
                world.WithTextFile(args[Array.IndexOf(args, "--output") + 1], empty ? "" : raw);
            }
        });
        var result = await CreateHost(world).RunAsync();
        await Assert.That(result.Exception is CakeException cake ? cake.ExitCode : 0).IsEqualTo(expectedExit);
        await Assert.That(result.Success).IsEqualTo(expectedExit == 0);
        await Assert.That(world.ProcessInvocations.Count).IsEqualTo(2);

        if (expectedExit != 0)
        {
            await Assert.That(result.Exception!.Message).IsEqualTo("Binding generation failed; see artifacts/binding-generation/generation-run.md.");
            await Assert.That(world.ReadAllText(root + "/sentinel.g.cs")).IsEqualTo("previous bytes");
            await Assert.That(world.FileExists(root + "/Compat/SDL_image.g.cs")).IsFalse();
        }
        else
        {
            await Assert.That(world.ReadAllText(root + "/Compat/SDL_image.g.cs")).IsEqualTo(empty ? "" : raw);
            await Assert.That(world.ReadAllText(root + "/Modern/SDL_image.g.cs")).IsEqualTo(empty ? "" : raw);
        }

        await Assert.That(world.ReadAllText("artifacts/binding-generation/generation-run.md"))
            .Contains("Generation exit code: " + expectedExit.ToString(CultureInfo.InvariantCulture));
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task RunAsync_Should_Generate_Both_Backends_And_Publish_Only_The_Selected_Family(bool evidence)
    {
        var raw = FixtureLoader.Load("BindingGeneration/Generation/image-neutral.cs.txt");
        var image = "spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Generated";
        var core = "spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/keep.g.cs";
        var world = FakeCakeWorld.CreateLinux()
            .WithManifestObject(ManifestFixture.RealManifest)
            .WithScope("sdl2-image")
            .WithGeneration()
            .WithEvidence(evidence)
            .WithTextFile("vcpkg_installed/x64-linux-hybrid/include/SDL2/SDL_image.h", "header")
            .WithTextFile(image + "/old.g.cs", "stale")
            .WithTextFile(core, "core sentinel")
            .WithToolPath("/usr/bin/dotnet")
            .WithProcessResult("dotnet", 0, "");
        var actual = new List<string[]>();
        world.CakeContext.ProcessRunner.When(runner => runner.Start(Arg.Any<FilePath>(), Arg.Any<ProcessSettings>())).Do(call =>
        {
            var settings = call.Arg<ProcessSettings>();
            var args = settings.Arguments.Select(argument => CommandLineParser.SplitCommandLineIntoArguments(argument.Render(), false).Single()).ToArray();
            actual.Add(args);
            world.WithTextFile(args[Array.IndexOf(args, "--output") + 1], raw);
        });

        var result = await CreateHost(world).RunAsync();
        await Assert.That(result.Exception).IsNull();
        await Assert.That(result.Success).IsTrue();
        await Assert.That(world.ReadAllText(image + "/Compat/SDL_image.g.cs")).IsEqualTo(raw);
        await Assert.That(world.ReadAllText(image + "/Modern/SDL_image.g.cs")).IsEqualTo(raw);
        await Assert.That(world.FileExists(image + "/old.g.cs")).IsFalse();
        await Assert.That(world.ReadAllText(core)).IsEqualTo("core sentinel");
        await Assert.That(actual.Select(args => args[4])).IsEquivalentTo(["compatible-codegen", "latest-codegen"], CollectionOrdering.Matching);
        await Assert.That(world.ReadAllText("artifacts/binding-generation/generation-run.md")).Contains("Exit code: 0");
        await Assert.That(world.FileExists("artifacts/binding-generation/oracle-evidence.md")).IsEqualTo(evidence);
    }

    internal static TargetTestHost<GenerateBindingsTask> CreateHost(FakeCakeWorld world)
        => new TargetTestHost<GenerateBindingsTask>(world).WithServices(services => services.AddData().AddValidators());

    private static string[] Tokens(ProcessSettings settings)
        => [.. settings.Arguments.Select(argument => CommandLineParser.SplitCommandLineIntoArguments(argument.Render(), false).Single())];
}
