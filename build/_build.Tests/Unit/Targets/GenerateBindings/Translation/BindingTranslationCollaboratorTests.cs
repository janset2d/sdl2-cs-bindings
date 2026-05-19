using System.Collections.Immutable;
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

    [Test]
    public async Task Extract_Should_Preserve_Pointer_Metadata_When_Classifying_Char_Pointer()
    {
        var translator = CreateTranslator();
        var compilation = new CppCompilation();
        var setHint = SdlFunction("SDL_SetHint", "SDL_hints.h");
        setHint.Parameters.Add(new CppParameter(new CppPointerType(CppPrimitiveType.Char), "name"));
        compilation.Functions.Add(setHint);

        var function = translator.Extract([compilation]).Single();

        await Assert.That(function.Parameters.Single().Type.Kind).IsEqualTo(NativeTypeKind.Utf8Pointer);
        await Assert.That(function.Parameters.Single().Type.ManagedName).IsEqualTo("byte*");
        await Assert.That(function.Parameters.Single().Type.PointerDepth).IsEqualTo(1);
    }

    [Test]
    public async Task Extract_Should_Map_Hid_Open_Erased_Wide_String_Parameter_To_Opaque_Pointer()
    {
        var translator = CreateTranslator();
        var compilation = new CppCompilation();
        var hidOpen = SdlFunction("SDL_hid_open", "SDL_hidapi.h");
        hidOpen.Parameters.Add(new CppParameter(CppPrimitiveType.UnsignedShort, "vendor_id"));
        hidOpen.Parameters.Add(new CppParameter(CppPrimitiveType.UnsignedShort, "product_id"));
        hidOpen.Parameters.Add(new CppParameter(new CppPointerType(CppPrimitiveType.Int), "serial_number"));
        compilation.Functions.Add(hidOpen);

        var function = translator.Extract([compilation]).Single();

        await Assert.That(function.Parameters.Single(parameter => parameter.Name == "serial_number").Type.ManagedName).IsEqualTo("nint");
    }

    [Test]
    public async Task Extract_Should_Not_Map_Arbitrary_Int_Pointer_Parameters_To_Opaque_Pointer()
    {
        var translator = CreateTranslator();
        var compilation = new CppCompilation();
        var function = SdlFunction("SDL_ReadPixels", "SDL_render.h");
        function.Parameters.Add(new CppParameter(new CppPointerType(CppPrimitiveType.Int), "pitch"));
        compilation.Functions.Add(function);

        var translated = translator.Extract([compilation]).Single();

        await Assert.That(translated.Parameters.Single().Type.ManagedName).IsEqualTo("int*");
    }

    private static BindingFunctionTranslator CreateTranslator() =>
        new(new BindableDeclarationPolicy(DefaultConfig, new KnownUnsupportedDeclarationPolicy(DefaultConfig)),
            new NativeTypeClassifier(NativeTypeClassificationContext.FromConfig(DefaultConfig)));

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

public sealed class RequiredConstantTranslatorTests
{
    [Test]
    public async Task Translate_Should_Model_Utf8_Literal_Constant_As_Managed_Span_Metadata()
    {
        var constant = BindingGenerationFixture.RequiredConstant(
            name: "SDL_HINT_RENDER_DRIVER",
            type: "ReadOnlySpan<byte>",
            value: "\"SDL_RENDER_DRIVER\"u8",
            sourceHeader: "SDL_hints.h");

        var sut = RequiredConstantTranslator.Translate([constant]).Single();

        await Assert.That(sut.Type.Kind).IsEqualTo(NativeTypeKind.SubstitutedManagedType);
        await Assert.That(sut.Type.NativeName).IsEqualTo("const char[]");
        await Assert.That(sut.Type.ManagedName).IsEqualTo("ReadOnlySpan<byte>");
        await Assert.That(sut.Type.PointerDepth).IsEqualTo(0);
        await Assert.That(sut.Type.AbiShape.StorageName).IsEqualTo("ReadOnlySpan<byte>");
        await Assert.That(sut.Type.AbiShape.SizeBytes).IsEqualTo(IntPtr.Size * 2);
        await Assert.That(sut.Type.AbiShape.IsBlittable).IsFalse();
    }
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
            new BindingFunction("SDL_Init", BindingGenerationFixture.NativeInt(), [], "SDL.h"),
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
        var classifier = new NativeTypeClassifier(NativeTypeClassificationContext.FromConfig(DefaultConfig));
        return new NeutralFunctionSetBuilder(new BindingFunctionTranslator(policy, classifier));
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

public sealed class BindingHandleTranslatorTests
{
    private static readonly BindingGenerationConfig DefaultConfig = BindingGenerationFixture.Sdl2CoreConfig();

    [Test]
    public async Task Extract_Should_Create_Handles_From_Opaque_Owned_Structs()
    {
        var translator = CreateTranslator();
        var window = new CppClass("SDL_Window")
        {
            ClassKind = CppClassKind.Struct,
            IsDefinition = false,
            Span = SdlHeaderSpan("SDL_video.h"),
        };
        var rect = new CppClass("SDL_Rect")
        {
            ClassKind = CppClassKind.Struct,
            IsDefinition = true,
            Span = SdlHeaderSpan("SDL_rect.h"),
        };
        rect.Fields.Add(new CppField(CppPrimitiveType.Int, "x"));
        var external = new CppClass("VkInstance_T")
        {
            ClassKind = CppClassKind.Struct,
            IsDefinition = false,
            Span = SdlHeaderSpan("SDL_vulkan.h"),
        };
        var nonSdlHeader = new CppClass("SDL_Pretender")
        {
            ClassKind = CppClassKind.Struct,
            IsDefinition = false,
            Span = HeaderSpan("C:/vendor/include/not-sdl/SDL_video.h"),
        };

        var handles = translator.Extract(new NativeDeclarationCatalog([], [window, rect, external, nonSdlHeader], [], []));

        var handle = await Assert.That(handles).HasSingleItem();
        await Assert.That(handle.Name).IsEqualTo("SDL_Window");
        await Assert.That(handle.Type.Kind).IsEqualTo(NativeTypeKind.OpaqueHandle);
        await Assert.That(handle.Type.SourceHeader).IsEqualTo("SDL_video.h");
        await Assert.That(handle.Type.OwningFamilyId).IsEqualTo("sdl2-core");
    }

    [Test]
    public async Task Extract_Should_Create_Handle_From_Private_Tag_Typedef()
    {
        var translator = CreateTranslator();
        var privateTag = new CppClass("_SDL_GameController")
        {
            ClassKind = CppClassKind.Struct,
            IsDefinition = false,
            Span = SdlHeaderSpan("SDL_gamecontroller.h"),
        };
        var gameController = new CppTypedef("SDL_GameController", privateTag)
        {
            Span = SdlHeaderSpan("SDL_gamecontroller.h"),
        };

        var handles = translator.Extract(new NativeDeclarationCatalog([], [], [], [gameController]));

        var handle = await Assert.That(handles).HasSingleItem();
        await Assert.That(handle.Name).IsEqualTo("SDL_GameController");
        await Assert.That(handle.Type.Kind).IsEqualTo(NativeTypeKind.OpaqueHandle);
        await Assert.That(handle.Type.NativeName).IsEqualTo("SDL_GameController");
        await Assert.That(handle.Type.ManagedName).IsEqualTo("SDL_GameController");
    }

    [Test]
    public async Task Extract_Should_Prefer_Public_Typedef_Name_Over_Opaque_Struct_Tag()
    {
        var translator = CreateTranslator();
        var hidTag = new CppClass("SDL_hid_device_")
        {
            ClassKind = CppClassKind.Struct,
            IsDefinition = false,
            Span = SdlHeaderSpan("SDL_hidapi.h"),
        };
        var hidTypedef = new CppTypedef("SDL_hid_device", hidTag)
        {
            Span = SdlHeaderSpan("SDL_hidapi.h"),
        };
        var semaphoreTag = new CppClass("SDL_semaphore")
        {
            ClassKind = CppClassKind.Struct,
            IsDefinition = false,
            Span = SdlHeaderSpan("SDL_mutex.h"),
        };
        var semaphoreTypedef = new CppTypedef("SDL_sem", semaphoreTag)
        {
            Span = SdlHeaderSpan("SDL_mutex.h"),
        };

        var handles = translator.Extract(new NativeDeclarationCatalog([], [hidTag, semaphoreTag], [], [hidTypedef, semaphoreTypedef]));

        await Assert.That(handles.Select(handle => handle.Name).ToArray())
            .IsEquivalentTo(["SDL_hid_device", "SDL_sem"]);
    }

    [Test]
    public async Task Extract_Should_Exclude_Deferred_Handle_Declarations()
    {
        var translator = CreateTranslator();
        var sysWmInfo = new CppClass("SDL_SysWMinfo")
        {
            ClassKind = CppClassKind.Struct,
            IsDefinition = false,
            Span = SdlHeaderSpan("SDL_syswm.h"),
        };
        var window = new CppClass("SDL_Window")
        {
            ClassKind = CppClassKind.Struct,
            IsDefinition = false,
            Span = SdlHeaderSpan("SDL_video.h"),
        };

        var handles = translator.Extract(new NativeDeclarationCatalog([], [sysWmInfo, window], [], []));

        var handle = await Assert.That(handles).HasSingleItem();
        await Assert.That(handle.Name).IsEqualTo("SDL_Window");
    }

    private static BindingHandleTranslator CreateTranslator(BindingGenerationConfig? config = null)
    {
        config ??= DefaultConfig;
        return new(CreatePolicy(config), new NativeTypeClassifier(NativeTypeClassificationContext.FromConfig(config)));
    }

    private static BindableDeclarationPolicy CreatePolicy(BindingGenerationConfig config) =>
        new(config, new KnownUnsupportedDeclarationPolicy(config));

    private static CppSourceSpan SdlHeaderSpan(string headerName) =>
        HeaderSpan($"C:/vcpkg/installed/x64-linux-hybrid/include/SDL2/{headerName}");

    private static CppSourceSpan HeaderSpan(string sourceFile) =>
        new(
            new CppSourceLocation(sourceFile, 0, 1, 1),
            new CppSourceLocation(sourceFile, 1, 1, 2));
}

public sealed class BindingEnumTranslatorTests
{
    private static readonly BindingGenerationConfig DefaultConfig = BindingGenerationFixture.Sdl2CoreConfig();

    [Test]
    public async Task Extract_Should_Create_Flags_Enum_With_UInt_Underlying_Type()
    {
        var translator = CreateTranslator();
        var windowFlags = new CppEnum("SDL_WindowFlags")
        {
            IntegerType = CppPrimitiveType.UnsignedInt,
            Span = SdlHeaderSpan("SDL_video.h"),
        };
        windowFlags.Items.Add(new CppEnumItem("SDL_WINDOW_FULLSCREEN", 1));
        windowFlags.Items.Add(new CppEnumItem("SDL_WINDOW_OPENGL", 2));

        var enumerations = translator.Extract([windowFlags]);

        var enumeration = await Assert.That(enumerations).HasSingleItem();
        await Assert.That(enumeration.Name).IsEqualTo("SDL_WindowFlags");
        await Assert.That(enumeration.IsFlags).IsTrue();
        await Assert.That(enumeration.UnderlyingType.NativeName).IsEqualTo("unsigned int");
        await Assert.That(enumeration.UnderlyingType.ManagedName).IsEqualTo("uint");
        await Assert.That(enumeration.UnderlyingType.SourceHeader).IsEqualTo("SDL_video.h");
        await Assert.That(enumeration.Members.Select(member => member.Name).ToArray())
            .IsEquivalentTo(["SDL_WINDOW_FULLSCREEN", "SDL_WINDOW_OPENGL"]);
        await Assert.That(enumeration.Members.Select(member => member.Value).ToArray())
            .IsEquivalentTo(["1", "2"]);
    }

    [Test]
    public async Task Extract_Should_Format_Same_Enum_Binary_Expressions_As_CSharp_Expressions()
    {
        var translator = CreateTranslator();
        var windowFlags = new CppEnum("SDL_WindowFlags")
        {
            IntegerType = CppPrimitiveType.UnsignedInt,
            Span = SdlHeaderSpan("SDL_video.h"),
        };
        windowFlags.Items.Add(new CppEnumItem("SDL_WINDOW_FULLSCREEN", 0x00000001));
        windowFlags.Items.Add(new CppEnumItem("SDL_WINDOW_FULLSCREEN_DESKTOP", 0x00001001)
        {
            ValueExpression = BinaryExpression(
                "|",
                IdentifierExpression("SDL_WINDOW_FULLSCREEN"),
                LiteralExpression("0x00001000")),
        });

        var enumeration = translator.Extract([windowFlags]).Single();

        await Assert.That(enumeration.Members.Single(member => member.Name == "SDL_WINDOW_FULLSCREEN_DESKTOP").Value)
            .IsEqualTo("SDL_WINDOW_FULLSCREEN | 0x00001000");
    }

    [Test]
    public async Task Extract_Should_Use_Numeric_Value_When_Expression_References_External_Enum_Member()
    {
        var translator = CreateTranslator();
        var keyCode = new CppEnum("SDL_KeyCode")
        {
            IntegerType = CppPrimitiveType.UnsignedInt,
            Span = SdlHeaderSpan("SDL_keycode.h"),
        };
        keyCode.Items.Add(new CppEnumItem("SDLK_CAPSLOCK", 1073741881)
        {
            ValueExpression = BinaryExpression(
                "|",
                IdentifierExpression("SDL_SCANCODE_CAPSLOCK"),
                ParenExpression(BinaryExpression(
                    "<<",
                    LiteralExpression("1"),
                    LiteralExpression("30")))),
        });

        var enumeration = translator.Extract([keyCode]).Single();

        await Assert.That(enumeration.Members.Single().Value).IsEqualTo("1073741881");
    }

    [Test]
    public async Task Extract_Should_Preserve_Enum_Member_Declaration_Order()
    {
        var translator = CreateTranslator();
        var eventType = new CppEnum("SDL_EventType")
        {
            IntegerType = CppPrimitiveType.Int,
            Span = SdlHeaderSpan("SDL_events.h"),
        };
        eventType.Items.Add(new CppEnumItem("SDL_LASTEVENT", 0xFFFF));
        eventType.Items.Add(new CppEnumItem("SDL_FIRSTEVENT", 0));

        var enumeration = translator.Extract([eventType]).Single();

        await Assert.That(string.Join(',', enumeration.Members.Select(member => member.Name)))
            .IsEqualTo("SDL_LASTEVENT,SDL_FIRSTEVENT");
    }

    [Test]
    public async Task Extract_Should_Force_SDL2_Bool_To_Int_Underlying_Type()
    {
        var translator = CreateTranslator();
        var sdlBool = new CppEnum("SDL_bool")
        {
            IntegerType = CppPrimitiveType.UnsignedInt,
            Span = SdlHeaderSpan("SDL_stdinc.h"),
        };
        sdlBool.Items.Add(new CppEnumItem("SDL_FALSE", 0));
        sdlBool.Items.Add(new CppEnumItem("SDL_TRUE", 1));

        var enumeration = translator.Extract([sdlBool]).Single();

        await Assert.That(enumeration.UnderlyingType.ManagedName).IsEqualTo("int");
    }

    [Test]
    public async Task Extract_Should_Mark_Known_SDL_Bitmask_Enums_As_Flags()
    {
        var translator = CreateTranslator();
        var keymod = SdlEnum("SDL_Keymod", "SDL_keycode.h");
        var glContextFlag = SdlEnum("SDL_GLcontextFlag", "SDL_video.h");
        var rendererFlip = SdlEnum("SDL_RendererFlip", "SDL_render.h");

        var enumerations = translator.Extract([keymod, glContextFlag, rendererFlip]);

        await Assert.That(enumerations.Single(enumeration => enumeration.Name == "SDL_Keymod").IsFlags).IsTrue();
        await Assert.That(enumerations.Single(enumeration => enumeration.Name == "SDL_GLcontextFlag").IsFlags).IsTrue();
        await Assert.That(enumerations.Single(enumeration => enumeration.Name == "SDL_RendererFlip").IsFlags).IsTrue();
    }

    [Test]
    public async Task Extract_Should_Exclude_NonOwned_Or_NonSdl_Header_Enums()
    {
        var translator = CreateTranslator();
        var sdl = SdlEnum("SDL_EventType", "SDL_events.h");
        var externalName = SdlEnum("VkResult", "SDL_vulkan.h");
        var nonSdlHeader = new CppEnum("SDL_Pretender")
        {
            IntegerType = CppPrimitiveType.Int,
            Span = HeaderSpan("C:/vendor/include/not-sdl/SDL_events.h"),
        };

        var enumerations = translator.Extract([sdl, externalName, nonSdlHeader]);

        var enumeration = await Assert.That(enumerations).HasSingleItem();
        await Assert.That(enumeration.Name).IsEqualTo("SDL_EventType");
    }

    [Test]
    public async Task Extract_Should_Exclude_Deferred_Enum_Declarations()
    {
        var config = DefaultConfig with
        {
            DeferredDeclarations = ImmutableDictionary<string, DeferredDeclarationConfig>.Empty
                .Add("SDL_DUMMY_ENUM", new DeferredDeclarationConfig
                {
                    Category = "internal-sdl-sentinel",
                    Reason = "SDL compile-time enum-size sentinel, not public API.",
                }),
        };
        var translator = CreateTranslator(config);
        var publicEnum = SdlEnum("SDL_EventType", "SDL_events.h");
        var dummyEnum = SdlEnum("SDL_DUMMY_ENUM", "SDL_stdinc.h");

        var enumerations = translator.Extract([dummyEnum, publicEnum]);

        var enumeration = await Assert.That(enumerations).HasSingleItem();
        await Assert.That(enumeration.Name).IsEqualTo("SDL_EventType");
    }

    private static BindingEnumTranslator CreateTranslator(BindingGenerationConfig? config = null)
    {
        config ??= DefaultConfig;
        return new(CreatePolicy(config), new NativeTypeClassifier(NativeTypeClassificationContext.FromConfig(config)));
    }

    private static BindableDeclarationPolicy CreatePolicy(BindingGenerationConfig config) =>
        new(config, new KnownUnsupportedDeclarationPolicy(config));

    private static CppEnum SdlEnum(string name, string headerName)
    {
        var enumeration = new CppEnum(name)
        {
            IntegerType = CppPrimitiveType.Int,
            Span = SdlHeaderSpan(headerName),
        };
        enumeration.Items.Add(new CppEnumItem(name + "_VALUE", 0));
        return enumeration;
    }

    private static CppRawExpression IdentifierExpression(string identifier) =>
        new(CppExpressionKind.DeclRef) { Text = identifier };

    private static CppLiteralExpression LiteralExpression(string value) =>
        new(CppExpressionKind.IntegerLiteral, value);

    private static CppBinaryExpression BinaryExpression(string op, params CppExpression[] arguments) =>
        new(CppExpressionKind.BinaryOperator)
        {
            Operator = op,
            Arguments = [.. arguments],
        };

    private static CppParenExpression ParenExpression(CppExpression expression) =>
        new()
        {
            Arguments = [expression],
        };

    private static CppSourceSpan SdlHeaderSpan(string headerName) =>
        HeaderSpan($"C:/vcpkg/installed/x64-linux-hybrid/include/SDL2/{headerName}");

    private static CppSourceSpan HeaderSpan(string sourceFile) =>
        new(
            new CppSourceLocation(sourceFile, 0, 1, 1),
            new CppSourceLocation(sourceFile, 1, 1, 2));
}

public sealed class BindingCallbackTranslatorTests
{
    private static readonly BindingGenerationConfig DefaultConfig = BindingGenerationFixture.Sdl2CoreConfig();

    [Test]
    public async Task Extract_Should_Create_Callback_From_Function_Pointer_Typedef()
    {
        var translator = CreateTranslator();
        var functionType = new CppFunctionType(CppPrimitiveType.Void);
        functionType.Parameters.Add(new CppParameter(new CppPointerType(CppPrimitiveType.Void), "userdata"));
        functionType.Parameters.Add(new CppParameter(new CppPointerType(new CppTypedef("Uint8", CppPrimitiveType.UnsignedChar)), "stream"));
        functionType.Parameters.Add(new CppParameter(CppPrimitiveType.Int, "len"));
        var callback = new CppTypedef("SDL_AudioCallback", new CppPointerType(functionType))
        {
            Span = SdlHeaderSpan("SDL_audio.h"),
        };
        var external = new CppTypedef("VkCallback", new CppPointerType(functionType))
        {
            Span = SdlHeaderSpan("SDL_vulkan.h"),
        };
        var nonSdlHeader = new CppTypedef("SDL_PretenderCallback", new CppPointerType(functionType))
        {
            Span = HeaderSpan("C:/vendor/include/not-sdl/SDL_audio.h"),
        };

        var callbacks = translator.Extract([callback, external, nonSdlHeader, new CppTypedef("Uint8", CppPrimitiveType.UnsignedChar)]);

        var bindingCallback = await Assert.That(callbacks).HasSingleItem();
        await Assert.That(bindingCallback.Name).IsEqualTo("SDL_AudioCallback");
        await Assert.That(bindingCallback.ReturnType.ManagedName).IsEqualTo("void");
        await Assert.That(string.Join(',', bindingCallback.Parameters.Select(parameter => parameter.Name)))
            .IsEqualTo("userdata,stream,len");
        await Assert.That(string.Join(',', bindingCallback.Parameters.Select(parameter => parameter.Type.ManagedName)))
            .IsEqualTo("nint,byte*,int");
    }

    [Test]
    public async Task Extract_Should_Exclude_Deferred_Callback_Typedefs()
    {
        var config = DefaultConfig with
        {
            DeferredDeclarations = ImmutableDictionary<string, DeferredDeclarationConfig>.Empty
                .Add("SDL_AudioCallback", new DeferredDeclarationConfig
                {
                    Category = "test-defer",
                    Reason = "exercised by callback translator test.",
                }),
        };
        var translator = CreateTranslator(config);
        var functionType = new CppFunctionType(CppPrimitiveType.Void);
        var deferredCallback = new CppTypedef("SDL_AudioCallback", new CppPointerType(functionType))
        {
            Span = SdlHeaderSpan("SDL_audio.h"),
        };
        var eventFilter = new CppTypedef("SDL_EventFilter", new CppPointerType(functionType))
        {
            Span = SdlHeaderSpan("SDL_events.h"),
        };

        var callbacks = translator.Extract([deferredCallback, eventFilter]);

        var callback = await Assert.That(callbacks).HasSingleItem();
        await Assert.That(callback.Name).IsEqualTo("SDL_EventFilter");
    }

    private static BindingCallbackTranslator CreateTranslator(BindingGenerationConfig? config = null)
    {
        config ??= DefaultConfig;
        return new(CreatePolicy(config), new NativeTypeClassifier(NativeTypeClassificationContext.FromConfig(config)));
    }

    private static BindableDeclarationPolicy CreatePolicy(BindingGenerationConfig config) =>
        new(config, new KnownUnsupportedDeclarationPolicy(config));

    private static CppSourceSpan SdlHeaderSpan(string headerName) =>
        HeaderSpan($"C:/vcpkg/installed/x64-linux-hybrid/include/SDL2/{headerName}");

    private static CppSourceSpan HeaderSpan(string sourceFile) =>
        new(
            new CppSourceLocation(sourceFile, 0, 1, 1),
            new CppSourceLocation(sourceFile, 1, 1, 2));
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

public sealed class StructFieldTranslatorTests
{
    private static readonly BindingGenerationConfig DefaultConfig = BindingGenerationFixture.Sdl2CoreConfig();

    [Test]
    public async Task Translate_Should_Map_Fixed_Buffer_And_Explicit_Offset()
    {
        var translator = CreateTranslator();
        var fixedBuffer = translator.Translate(
            new CppField(new CppArrayType(CppPrimitiveType.UnsignedChar, 16), "data"),
            LayoutKind.Sequential);
        var explicitField = translator.Translate(
            new CppField(CppPrimitiveType.Int, "button") { Offset = 4 },
            LayoutKind.Explicit);

        await Assert.That(fixedBuffer.Name).IsEqualTo("data");
        await Assert.That(fixedBuffer.Type.ManagedName).IsEqualTo("byte");
        await Assert.That(fixedBuffer.FixedBufferLength).IsEqualTo(16);
        await Assert.That(fixedBuffer.FieldOffset).IsNull();
        await Assert.That(explicitField.FieldOffset).IsEqualTo(4);
    }

    [Test]
    public async Task Translate_Should_Map_Hid_Info_Erased_Wide_String_Field_To_Opaque_Pointer()
    {
        var translator = CreateTranslator();

        var field = translator.Translate(
            new CppField(new CppPointerType(CppPrimitiveType.Int), "serial_number"),
            parentStructName: "SDL_hid_device_info",
            LayoutKind.Sequential,
            addNestedStruct: null,
            sourceHeader: "SDL_hidapi.h");

        await Assert.That(field.Type.ManagedName).IsEqualTo("nint");
    }

    [Test]
    public async Task Translate_Should_Not_Map_Arbitrary_Int_Pointer_Fields_To_Opaque_Pointer()
    {
        var translator = CreateTranslator();

        var field = translator.Translate(
            new CppField(new CppPointerType(CppPrimitiveType.Int), "values"),
            parentStructName: "SDL_Custom",
            LayoutKind.Sequential,
            addNestedStruct: null,
            sourceHeader: "SDL_custom.h");

        await Assert.That(field.Type.ManagedName).IsEqualTo("int*");
    }

    private static StructFieldTranslator CreateTranslator() =>
        new(new NativeTypeClassifier(NativeTypeClassificationContext.FromConfig(DefaultConfig)));
}
