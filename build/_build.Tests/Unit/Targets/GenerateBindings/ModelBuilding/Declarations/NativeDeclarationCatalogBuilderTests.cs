using Build.Data.BindingGeneration.Models;
using Build.Targets.GenerateBindings.ModelBuilding.Declarations;
using Build.Targets.GenerateBindings.Parse;
using Build.Targets.GenerateBindings.PlatformViews;
using Build.Tests.Fixtures;
using CppAst;

namespace Build.Tests.Unit.Targets.GenerateBindings.ModelBuilding.Declarations;

public sealed class NativeDeclarationCatalogBuilderTests
{
    private static readonly BindingGenerationConfig DefaultConfig = BindingGenerationFixture.Sdl2CoreConfig();

    [Test]
    public async Task Build_Should_Filter_Functions_And_Deduplicate_Declarations_By_Source_File_And_Name()
    {
        var policy = new BindableDeclarationPolicy(DefaultConfig, new KnownUnsupportedDeclarationPolicy(DefaultConfig));
        var builder = new NativeDeclarationCatalogBuilder(policy);
        var firstCompilation = new CppCompilation();
        firstCompilation.Functions.Add(SdlFunction("SDL_GetTicks", "SDL_timer.h"));
        firstCompilation.Functions.Add(SdlFunction("SDL_main", "SDL_main.h"));
        firstCompilation.Classes.Add(SdlClass("SDL_Rect", "SDL_rect.h"));
        firstCompilation.Enums.Add(SdlEnum("SDL_EventType", "SDL_events.h"));
        firstCompilation.Typedefs.Add(SdlTypedef("SDL_AudioFormat", "SDL_audio.h"));

        var secondCompilation = new CppCompilation();
        secondCompilation.Functions.Add(SdlFunction("SDL_GetTicks", "SDL_timer.h"));
        secondCompilation.Classes.Add(SdlClass("SDL_Rect", "SDL_rect.h"));
        secondCompilation.Enums.Add(SdlEnum("SDL_EventType", "SDL_events.h"));
        secondCompilation.Typedefs.Add(SdlTypedef("SDL_AudioFormat", "SDL_audio.h"));

        var catalog = builder.Build([ParseResult("Neutral", firstCompilation), ParseResult("Linux", secondCompilation, "linux")]);

        await Assert.That(catalog.Functions.Select(function => function.Name).ToArray()).IsEquivalentTo(["SDL_GetTicks"]);
        await Assert.That(catalog.Classes.Select(cls => cls.Name).ToArray()).IsEquivalentTo(["SDL_Rect"]);
        await Assert.That(catalog.Enums.Select(enumeration => enumeration.Name).ToArray()).IsEquivalentTo(["SDL_EventType"]);
        await Assert.That(catalog.Typedefs.Select(typedef => typedef.Name).ToArray()).IsEquivalentTo(["SDL_AudioFormat"]);
    }

    private static CppAstParseResult ParseResult(string name, CppCompilation compilation, string? platform = null) =>
        new(
            new PlatformParseView(
                Name: name,
                Kind: platform is null ? PlatformConditionKind.Neutral : PlatformConditionKind.OperatingSystem,
                SupportedOsPlatform: platform,
                Defines: [],
                Undefines: []),
            [compilation]);

    private static CppFunction SdlFunction(string name, string headerName) =>
        new(name)
        {
            ReturnType = CppPrimitiveType.Int,
            Span = SdlHeaderSpan(headerName),
        };

    private static CppClass SdlClass(string name, string headerName) =>
        new(name)
        {
            ClassKind = CppClassKind.Struct,
            IsDefinition = true,
            Span = SdlHeaderSpan(headerName),
        };

    private static CppEnum SdlEnum(string name, string headerName) =>
        new(name)
        {
            Span = SdlHeaderSpan(headerName),
        };

    private static CppTypedef SdlTypedef(string name, string headerName) =>
        new(name, CppPrimitiveType.UnsignedShort)
        {
            Span = SdlHeaderSpan(headerName),
        };

    private static CppSourceSpan SdlHeaderSpan(string headerName) =>
        new(
            new CppSourceLocation($"C:/vcpkg/installed/x64-linux-hybrid/include/SDL2/{headerName}", 0, 1, 1),
            new CppSourceLocation($"C:/vcpkg/installed/x64-linux-hybrid/include/SDL2/{headerName}", 1, 1, 2));
}
