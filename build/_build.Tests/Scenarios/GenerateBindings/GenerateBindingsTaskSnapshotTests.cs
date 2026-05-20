using System.Collections.Immutable;
using System.Text.Json;
using Build.Data.BindingGeneration;
using Build.Data.BindingGeneration.Models;
using Build.Results;
using Build.Targets.GenerateBindings;
using Build.Targets.GenerateBindings.Emit;
using Build.Targets.GenerateBindings.HeaderSet;
using Build.Targets.GenerateBindings.ModelBuilding;
using Build.Targets.GenerateBindings.Parse;
using Build.Targets.GenerateBindings.PlatformViews;
using Build.Tests.Fixtures;
using Build.Validation.BindingGeneration;
using Cake.Core.Diagnostics;
using CppAst;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using static Build.Tests.Fixtures.BindingGenerationFixture;

namespace Build.Tests.Scenarios.GenerateBindings;

public sealed class GenerateBindingsTaskSnapshotTests
{
    [Test]
    public async Task RunAsync_Should_Match_Semantic_Inventory_Output_Snapshot()
    {
        var world = FakeCakeWorld.CreateLinux();
        var config = Sdl2CoreConfig(
            requiredConstants:
            [
                RequiredConstant("SDL_INIT_TIMER"),
            ]) with
            {
                Validators = ImmutableDictionary<string, bool>.Empty,
            };
        world.WithTextFile("vcpkg_installed/x64-linux-hybrid/include/SDL2/SDL_video.h", string.Empty);
        var parser = Substitute.For<ICppAstParseRunner>();
        parser
            .Parse(Arg.Any<BindingGenerationConfig>(), Arg.Any<ResolvedHeaderSet>(), Arg.Any<PlatformParseView>())
            .Returns(call => new CppAstParseResult(call.Arg<PlatformParseView>(), [SemanticInventoryCompilation()]));

        var result = await CreateHost(world, parser, CreateEnabledRepository(config)).RunAsync();

        await Assert.That(result.Success).IsTrue();
        var generatedRoot = "artifacts/generated-bindings-preview/sdl2-core";
        var files = ReadGeneratedFiles(world, generatedRoot);
        var minimumExpectedFiles = new[]
        {
            "Constants.g.cs",
            "Types/Enums.g.cs",
            "Types/Handles.g.cs",
            "Types/Structs.g.cs",
            "Types/Callbacks.g.cs",
            "Platform/Neutral/Commands.g.cs",
            "parse-views.json",
        };
        foreach (var file in minimumExpectedFiles)
        {
            await Assert.That(files).Contains(file);
        }

        var snapshot = new TaskOutputSnapshot(
            Files: files,
            FileContents: ReadFileContents(world, generatedRoot, files),
            InformationMessages: result.Log.Entries
                .Where(entry => entry.Level == LogLevel.Information)
                .Select(entry => entry.Message)
                .ToArray());

        await Verify(snapshot);
    }

    private static TargetTestHost<GenerateBindingsTask> CreateHost(
        FakeCakeWorld world,
        ICppAstParseRunner parser,
        IBindingGenerationConfigRepository configRepository)
    {
        return new TargetTestHost<GenerateBindingsTask>(world)
            .WithServices(services =>
            {
                services.AddSingleton<ParseDiagnosticFormatter>();
                services.AddSingleton<HeaderSetResolver>();
                services.AddSingleton<BindingModelBuilder>();
                services.AddSingleton<BindingEmitter>();
                services.AddSingleton<BindingFamilyGeneration>();
                services.AddSingleton(Substitute.For<ILibclangVersionAsserter>());
                services.AddSingleton(parser);
                services.AddSingleton(configRepository);
            });
    }

    private static IBindingGenerationConfigRepository CreateEnabledRepository(BindingGenerationConfig config)
    {
        var repo = Substitute.For<IBindingGenerationConfigRepository>();
        repo.EnumerateEnabledFamilies().Returns([config.FamilyId]);
        repo.Load(config.FamilyId).Returns(
            Result<BindingGenerationConfig, BindingGenerationConfigError>.Success(config));
        return repo;
    }

    private static IReadOnlyList<string> ReadGeneratedFiles(FakeCakeWorld world, string generatedRoot)
    {
        var parseViews = world.ReadAllText($"{generatedRoot}/parse-views.json");
        using var document = JsonDocument.Parse(parseViews);
        return [.. document.RootElement.GetProperty("EmittedFiles")
            .EnumerateArray()
            .Select(element => element.GetString() ?? string.Empty)
            .OrderBy(file => file, StringComparer.Ordinal)];
    }

    private static SortedDictionary<string, string> ReadFileContents(FakeCakeWorld world, string generatedRoot, IEnumerable<string> files)
    {
        var contents = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in files)
        {
            contents[file] = NormalizeLineEndings(world.ReadAllText($"{generatedRoot}/{file}"));
        }

        return contents;
    }

    private static string NormalizeLineEndings(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal);

    private static CppCompilation SemanticInventoryCompilation()
    {
        var compilation = new CppCompilation();
        compilation.Functions.Add(SdlFunction("SDL_GetTicks", "SDL_timer.h"));
        compilation.Classes.Add(OpaqueStruct("SDL_Window", "SDL_video.h"));
        compilation.Classes.Add(RectStruct());
        compilation.Enums.Add(EventTypeEnum());
        compilation.Typedefs.Add(AudioCallbackTypedef());
        compilation.Macros.Add(SdlHintMacro());
        return compilation;
    }

    private static CppFunction SdlFunction(string name, string headerName) =>
        new(name)
        {
            ReturnType = CppPrimitiveType.Int,
            Span = SdlHeaderSpan(headerName),
        };

    private static CppClass OpaqueStruct(string name, string headerName) =>
        new(name)
        {
            ClassKind = CppClassKind.Struct,
            IsDefinition = false,
            Span = SdlHeaderSpan(headerName),
        };

    private static CppClass RectStruct()
    {
        var rect = new CppClass("SDL_Rect")
        {
            ClassKind = CppClassKind.Struct,
            IsDefinition = true,
            Span = SdlHeaderSpan("SDL_rect.h"),
        };
        rect.Fields.Add(new CppField(CppPrimitiveType.Int, "x"));
        return rect;
    }

    private static CppEnum EventTypeEnum()
    {
        var eventType = new CppEnum("SDL_EventType")
        {
            IntegerType = CppPrimitiveType.UnsignedInt,
            Span = SdlHeaderSpan("SDL_events.h"),
        };
        eventType.Items.Add(new CppEnumItem("SDL_QUIT", 0x100));
        return eventType;
    }

    private static CppTypedef AudioCallbackTypedef()
    {
        var functionType = new CppFunctionType(CppPrimitiveType.Void);
        return new CppTypedef("SDL_AudioCallback", new CppPointerType(functionType))
        {
            Span = SdlHeaderSpan("SDL_audio.h"),
        };
    }

    private static CppMacro SdlHintMacro()
    {
        var macro = new CppMacro("SDL_HINT_RENDER_DRIVER") { Value = "\"SDL_RENDER_DRIVER\"" };
        macro.Span = SdlHeaderSpan("SDL_hints.h");
        macro.Tokens.Add(new CppToken(CppTokenKind.Literal, "\"SDL_RENDER_DRIVER\""));
        return macro;
    }

    private static CppSourceSpan SdlHeaderSpan(string headerName) =>
        new(
            new CppSourceLocation($"C:/vcpkg/installed/x64-linux-hybrid/include/SDL2/{headerName}", 0, 1, 1),
            new CppSourceLocation($"C:/vcpkg/installed/x64-linux-hybrid/include/SDL2/{headerName}", 1, 1, 2));

    private sealed record TaskOutputSnapshot(
        IReadOnlyList<string> Files,
        IReadOnlyDictionary<string, string> FileContents,
        IReadOnlyList<string> InformationMessages);
}
