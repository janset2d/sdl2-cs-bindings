using System.Runtime.InteropServices;
using Build.Data.BindingGeneration.Models;
using Build.Targets.GenerateBindings.Model;
using Build.Targets.GenerateBindings.Parsing;
using Build.Targets.GenerateBindings.Translation;
using Build.Tests.Fixtures;
using CppAst;

namespace Build.Tests.Unit.Targets.GenerateBindings.Translation;

public sealed class BindableDeclarationPolicyTests
{
    private static readonly BindingGenerationConfig DefaultConfig = BindingGenerationFixture.Sdl2CoreConfig();

    [Test]
    public async Task IsBindableFunction_Should_Require_Sdl2_Source_Export_Name()
    {
        var policy = CreatePolicy();
        var valid = SdlFunction("SDL_GetTicks", "SDL_timer.h");
        var inline = SdlFunction("SDL_RectEmpty", "SDL_rect.h");
        inline.Flags = CppFunctionFlags.Inline;
        var excluded = SdlFunction("SDL_main", "SDL_main.h");
        var nonSdl = new CppFunction("SDL_GetTicks")
        {
            ReturnType = CppPrimitiveType.Int,
            Span = HeaderSpan("C:/vendor/include/not-sdl/SDL_timer.h"),
        };

        await Assert.That(policy.IsBindableFunction(valid)).IsTrue();
        await Assert.That(policy.IsBindableFunction(inline)).IsFalse();
        await Assert.That(policy.IsBindableFunction(excluded)).IsFalse();
        await Assert.That(policy.IsBindableFunction(nonSdl)).IsFalse();
    }

    private static BindableDeclarationPolicy CreatePolicy() =>
        new(DefaultConfig, new KnownUnsupportedDeclarationPolicy(DefaultConfig));

    private static CppFunction SdlFunction(string name, string headerName) =>
        new(name)
        {
            ReturnType = CppPrimitiveType.Int,
            Span = SdlHeaderSpan(headerName),
        };

    private static CppSourceSpan SdlHeaderSpan(string headerName) =>
        HeaderSpan($"C:/vcpkg/installed/x64-linux-hybrid/include/SDL2/{headerName}");

    private static CppSourceSpan HeaderSpan(string sourceFile) =>
        new(
            new CppSourceLocation(sourceFile, 0, 1, 1),
            new CppSourceLocation(sourceFile, 1, 1, 2));
}

public sealed class BindingFunctionTranslatorTests
{
    private static readonly BindingGenerationConfig DefaultConfig = BindingGenerationFixture.Sdl2CoreConfig();

    [Test]
    public async Task Extract_Should_Deduplicate_Functions_By_Source_File_And_Name()
    {
        var translator = CreateTranslator();
        var firstCompilation = new CppCompilation();
        firstCompilation.Functions.Add(SdlFunction("SDL_ShowWindow", "SDL_video.h"));
        firstCompilation.Functions.Add(SdlFunction("SDL_DestroyWindow", "SDL_video.h"));

        var secondCompilation = new CppCompilation();
        secondCompilation.Functions.Add(SdlFunction("SDL_ShowWindow", "SDL_video.h"));

        var functions = translator.Extract([firstCompilation, secondCompilation]);

        await Assert.That(functions.Select(f => f.Name).ToArray())
            .IsEquivalentTo(["SDL_DestroyWindow", "SDL_ShowWindow"]);
        await Assert.That(functions.Select(f => f.SourceHeader).ToArray())
            .IsEquivalentTo(["SDL_video.h", "SDL_video.h"]);
    }

    private static BindingFunctionTranslator CreateTranslator() =>
        new(new BindableDeclarationPolicy(DefaultConfig, new KnownUnsupportedDeclarationPolicy(DefaultConfig)));

    private static CppFunction SdlFunction(string name, string headerName) =>
        new(name)
        {
            ReturnType = CppPrimitiveType.Int,
            Span = SdlHeaderSpan(headerName),
        };

    private static CppSourceSpan SdlHeaderSpan(string headerName) =>
        new(
            new CppSourceLocation($"C:/vcpkg/installed/x64-linux-hybrid/include/SDL2/{headerName}", 0, 1, 1),
            new CppSourceLocation($"C:/vcpkg/installed/x64-linux-hybrid/include/SDL2/{headerName}", 1, 1, 2));
}

public sealed class NeutralFunctionSetBuilderTests
{
    private static readonly BindingGenerationConfig DefaultConfig = BindingGenerationFixture.Sdl2CoreConfig();

    [Test]
    public async Task Build_Should_Merge_Required_Function_Names_With_Parsed_Neutral_Functions()
    {
        var builder = CreateBuilder();
        var neutralCompilation = new CppCompilation();
        neutralCompilation.Functions.Add(SdlFunction("SDL_GetTicks", "SDL_timer.h"));

        var platformCompilation = new CppCompilation();
        platformCompilation.Functions.Add(SdlFunction("SDL_LinuxOnly", "SDL_system.h"));

        var required = new[]
        {
            new BindingFunction("SDL_Init", BindingTypeRef.Of("int"), [], "SDL.h"),
        };

        var names = builder.Build(
            [
                ParseResult("Neutral", neutralCompilation),
                ParseResult("Linux", platformCompilation, "linux"),
            ],
            required);

        await Assert.That(names).IsEquivalentTo(["SDL_GetTicks", "SDL_Init"]);
    }

    private static NeutralFunctionSetBuilder CreateBuilder()
    {
        var policy = new BindableDeclarationPolicy(DefaultConfig, new KnownUnsupportedDeclarationPolicy(DefaultConfig));
        return new NeutralFunctionSetBuilder(new BindingFunctionTranslator(policy));
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

    private static CppSourceSpan SdlHeaderSpan(string headerName) =>
        new(
            new CppSourceLocation($"C:/vcpkg/installed/x64-linux-hybrid/include/SDL2/{headerName}", 0, 1, 1),
            new CppSourceLocation($"C:/vcpkg/installed/x64-linux-hybrid/include/SDL2/{headerName}", 1, 1, 2));
}

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
        await Assert.That(value.Fields.Select(f => f.FieldOffset).ToArray()).IsEquivalentTo(new int?[] { 0, 0 });
    }

    private static BindingStructTranslator CreateTranslator()
    {
        var policy = new BindableDeclarationPolicy(DefaultConfig, new KnownUnsupportedDeclarationPolicy(DefaultConfig));
        return new BindingStructTranslator(policy);
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

public sealed class StructFieldTranslatorTests
{
    [Test]
    public async Task Translate_Should_Map_Fixed_Buffer_And_Explicit_Offset()
    {
        var fixedBuffer = StructFieldTranslator.Translate(
            new CppField(new CppArrayType(CppPrimitiveType.UnsignedChar, 16), "data"),
            LayoutKind.Sequential);
        var explicitField = StructFieldTranslator.Translate(
            new CppField(CppPrimitiveType.Int, "button") { Offset = 4 },
            LayoutKind.Explicit);

        await Assert.That(fixedBuffer.Name).IsEqualTo("data");
        await Assert.That(fixedBuffer.Type.ManagedName).IsEqualTo("byte");
        await Assert.That(fixedBuffer.FixedBufferLength).IsEqualTo(16);
        await Assert.That(fixedBuffer.FieldOffset).IsNull();
        await Assert.That(explicitField.FieldOffset).IsEqualTo(4);
    }
}
