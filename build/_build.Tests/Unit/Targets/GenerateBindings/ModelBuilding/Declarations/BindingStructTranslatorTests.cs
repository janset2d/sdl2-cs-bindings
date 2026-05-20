using System.Runtime.InteropServices;
using Build.Data.BindingGeneration.Models;
using Build.Targets.GenerateBindings.ModelBuilding.Declarations;
using Build.Targets.GenerateBindings.ModelBuilding.Types;
using Build.Targets.GenerateBindings.Parse;
using Build.Targets.GenerateBindings.PlatformViews;
using Build.Tests.Fixtures;
using CppAst;

namespace Build.Tests.Unit.Targets.GenerateBindings.ModelBuilding.Declarations;

public sealed class BindingStructTranslatorTests
{
    private static readonly BindingGenerationConfig DefaultConfig = BindingGenerationFixture.Sdl2CoreConfig();

    [Test]
    public async Task Extract_Should_Deduplicate_And_Order_Generic_Owned_Structs()
    {
        var translator = CreateTranslator();
        var firstCompilation = new CppCompilation();
        firstCompilation.Classes.Add(CustomPodStruct());

        var secondCompilation = new CppCompilation();
        secondCompilation.Classes.Add(CustomPodStruct());
        secondCompilation.Classes.Add(GameControllerUnion());

        var structs = translator.Extract(
            [
                ParseResult("Neutral", firstCompilation),
                ParseResult("Linux", secondCompilation, "linux"),
            ]);

        await Assert.That(structs.Select(s => s.Name).ToArray())
            .IsEquivalentTo(["SDL_CustomPod", "SDL_GameControllerButtonBind"]);
    }

    [Test]
    public async Task Extract_Should_Exclude_SDL_GUID_Because_It_Is_A_Substituted_Dotnet_Type()
    {
        var translator = CreateTranslator();
        var compilation = new CppCompilation();
        compilation.Classes.Add(GuidStruct());

        var structs = translator.Extract([ParseResult("Neutral", compilation)]);

        await Assert.That(structs.Select(s => s.Name).ToArray()).DoesNotContain("SDL_GUID");
    }

    [Test]
    public async Task Extract_Should_Model_Anonymous_Union_Field_As_Generated_Sibling_Type()
    {
        var translator = CreateTranslator();
        var compilation = new CppCompilation();
        compilation.Classes.Add(GameControllerButtonBindStructWithAnonymousValueUnion());

        var structs = translator.Extract([ParseResult("Neutral", compilation)]);
        var parent = structs.Single(s => s.Name == "SDL_GameControllerButtonBind");
        var value = structs.Single(s => s.Name == "SDL_GameControllerButtonBind_value");

        await Assert.That(parent.Layout).IsEqualTo(LayoutKind.Sequential);
        await Assert.That(parent.Fields.Select(f => f.Name).ToArray()).IsEquivalentTo(["bindType", "@value"]);
        await Assert.That(parent.Fields.Select(f => f.Type.ManagedName).ToArray())
            .IsEquivalentTo(["int", "SDL_GameControllerButtonBind_value"]);
        await Assert.That(value.Layout).IsEqualTo(LayoutKind.Explicit);
        await Assert.That(value.ExplicitSize).IsEqualTo(8);
        await Assert.That(value.Fields.Select(f => f.Name).ToArray()).IsEquivalentTo(["button", "axis"]);
        await Assert.That(value.Fields.Select(f => f.Type.SourceHeader ?? string.Empty).ToArray())
            .IsEquivalentTo(["SDL_gamecontroller.h", "SDL_gamecontroller.h"]);
        await Assert.That(value.Fields.Select(f => f.FieldOffset).ToArray()).IsEquivalentTo(new int?[] { 0, 0 });
    }

    [Test]
    public async Task Extract_Should_Not_Emit_SDL_RWops_As_Public_Struct()
    {
        var translator = CreateTranslator();
        var compilation = new CppCompilation();
        var rwops = new CppClass("SDL_RWops")
        {
            ClassKind = CppClassKind.Struct,
            IsDefinition = true,
            SizeOf = 88,
            Span = SdlHeaderSpan("SDL_rwops.h"),
        };
        rwops.Fields.Add(new CppField(CppPrimitiveType.Int, "type"));
        compilation.Classes.Add(rwops);

        var structs = translator.Extract([ParseResult("Neutral", compilation)]);

        await Assert.That(structs.Select(structure => structure.Name).ToArray()).DoesNotContain("SDL_RWops");
    }

    private static BindingStructTranslator CreateTranslator()
    {
        var policy = new BindableDeclarationPolicy(DefaultConfig, new KnownUnsupportedDeclarationPolicy(DefaultConfig));
        var classifier = new NativeTypeClassifier(NativeTypeClassificationContext.FromConfig(DefaultConfig));
        return new BindingStructTranslator(policy, classifier);
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

    private static CppClass GuidStruct()
    {
        var guid = new CppClass("SDL_GUID")
        {
            ClassKind = CppClassKind.Struct,
            IsDefinition = true,
            Span = SdlHeaderSpan("SDL_stdinc.h"),
        };
        guid.Fields.Add(new CppField(new CppArrayType(CppPrimitiveType.UnsignedChar, 16), "data"));
        return guid;
    }

    private static CppClass CustomPodStruct()
    {
        var custom = new CppClass("SDL_CustomPod")
        {
            ClassKind = CppClassKind.Struct,
            IsDefinition = true,
            Span = SdlHeaderSpan("SDL_custom.h"),
        };
        custom.Fields.Add(new CppField(CppPrimitiveType.Int, "width"));
        return custom;
    }

    private static CppClass GameControllerUnion()
    {
        var bind = new CppClass("SDL_GameControllerButtonBind")
        {
            ClassKind = CppClassKind.Union,
            IsDefinition = true,
            SizeOf = 4,
            Span = SdlHeaderSpan("SDL_gamecontroller.h"),
        };
        bind.Fields.Add(new CppField(CppPrimitiveType.Int, "button") { Offset = 0 });
        return bind;
    }

    private static CppClass GameControllerButtonBindStructWithAnonymousValueUnion()
    {
        var valueUnion = new CppClass(string.Empty)
        {
            ClassKind = CppClassKind.Union,
            IsDefinition = true,
            IsAnonymous = true,
            SizeOf = 8,
        };
        valueUnion.Fields.Add(new CppField(CppPrimitiveType.Int, "button") { Offset = 0 });
        valueUnion.Fields.Add(new CppField(CppPrimitiveType.Int, "axis") { Offset = 0 });

        var bind = new CppClass("SDL_GameControllerButtonBind")
        {
            ClassKind = CppClassKind.Struct,
            IsDefinition = true,
            SizeOf = 12,
            Span = SdlHeaderSpan("SDL_gamecontroller.h"),
        };
        bind.Fields.Add(new CppField(new CppEnum("SDL_GameControllerBindType"), "bindType"));
        bind.Fields.Add(new CppField(valueUnion, "value"));
        return bind;
    }

    private static CppSourceSpan SdlHeaderSpan(string headerName) =>
        new(
            new CppSourceLocation($"C:/vcpkg/installed/x64-linux-hybrid/include/SDL2/{headerName}", 0, 1, 1),
            new CppSourceLocation($"C:/vcpkg/installed/x64-linux-hybrid/include/SDL2/{headerName}", 1, 1, 2));
}
