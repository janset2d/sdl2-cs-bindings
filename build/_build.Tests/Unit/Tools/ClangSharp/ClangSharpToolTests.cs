using System.Text.Json;
using Build.Tests.Fixtures;
using Build.Tools.ClangSharp;
using Cake.Core.IO;
using Microsoft.CodeAnalysis;
using NSubstitute;
using TUnit.Assertions.Enums;

namespace Build.Tests.Unit.Tools.ClangSharp;

public sealed class ClangSharpToolTests
{
    [Test]
    public async Task RenderArguments_Should_Separate_Modes_And_Features_Before_Response_Files()
    {
        var tokens = JsonSerializer.Deserialize<string[]>(FixtureLoader.Load("ClangSharp/core-neutral-compat.json"))!;
        var settings = ClangSharpData.SettingsFrom(tokens) with { Config = ["codegen=compatible", "types=windows"] };

        var actual = ClangSharpTool.RenderArguments(settings)
            .Select(argument => CommandLineParser.SplitCommandLineIntoArguments(argument.Render(), removeHashComments: false).Single())
            .ToArray();

        await Assert.That(actual).IsEquivalentTo([
            "tool", "run", "ClangSharpPInvokeGenerator", "--config", "codegen=compatible", "types=windows",
            "--generate", "macro-bindings", "@{REPO}/build/binding-generation/rsp/base.rsp", "@{REPO}/build/binding-generation/rsp/sdl2-core.rsp",
            "--method-class-name", "SDLNative", "--library-path", "SDL2", "--namespace", "SDL2", "--with-access-specifier", "SDLNative=Internal",
            "--include-directory", "{REPO}/vcpkg_installed/x64-windows-hybrid/include/SDL2",
            "--file", "{REPO}/vcpkg_installed/x64-windows-hybrid/include/SDL2/SDL_assert.h",
            "--output", "{REPO}/spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Compat/SDL_assert.g.cs",
        ], CollectionOrdering.Matching);
    }

    [Test]
    [Arguments("core-neutral-compat")]
    [Arguments("core-neutral-modern")]
    [Arguments("core-per-header")]
    [Arguments("core-platform")]
    [Arguments("gfx-neutral")]
    public async Task RenderArguments_Should_Preserve_Recorded_Tokens(string scenario)
    {
        var expected = JsonSerializer.Deserialize<string[]>(FixtureLoader.Load($"ClangSharp/{scenario}.json"))!;
        var settings = ClangSharpData.SettingsFrom(expected);

        var actual = ClangSharpTool.RenderArguments(settings)
            .Select(argument => CommandLineParser.SplitCommandLineIntoArguments(argument.Render(), removeHashComments: false).Single())
            .ToArray();

        await Assert.That(actual).IsEquivalentTo(expected, CollectionOrdering.Matching);
    }

    [Test]
    [Arguments(0)]
    [Arguments(17)]
    public async Task Run_Should_Return_Exit_Code_And_Capture_Both_Streams(int exitCode)
    {
        var expected = JsonSerializer.Deserialize<string[]>(FixtureLoader.Load("ClangSharp/core-platform.json"))!;
        var settings = ClangSharpData.SettingsFrom(expected) with { WorkingDirectory = new DirectoryPath("/repo/working directory") };
        var world = FakeCakeWorld.CreateLinux()
            .WithToolPath("/usr/bin/dotnet")
            .WithProcessResult("dotnet", exitCode, "first\nsecond", "warning\nerror");
        ProcessSettings? started = null;
        world.CakeContext.ProcessRunner
            .When(runner => runner.Start(Arg.Any<FilePath>(), Arg.Any<ProcessSettings>()))
            .Do(call => started = call.Arg<ProcessSettings>());

        var result = ClangSharpTool.Run(world.CakeContext, settings);

        await Assert.That(result.ExitCode).IsEqualTo(exitCode);
        await Assert.That(result.StandardOutput).IsEquivalentTo(["first", "second"], CollectionOrdering.Matching);
        await Assert.That(result.StandardError).IsEquivalentTo(["warning", "error"], CollectionOrdering.Matching);
        var invocation = world.ProcessInvocations.Single();
        var invoked = started!.Arguments
            .Select(argument => CommandLineParser.SplitCommandLineIntoArguments(argument.Render(), removeHashComments: false).Single())
            .ToArray();
        await Assert.That(invoked).IsEquivalentTo(expected, CollectionOrdering.Matching);
        await Assert.That(started.WorkingDirectory.FullPath).IsEqualTo(settings.WorkingDirectory.FullPath);
        await Assert.That(invocation.RedirectStandardOutput).IsTrue();
        await Assert.That(invocation.RedirectStandardError).IsTrue();
    }
}
