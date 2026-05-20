using System.Collections.Immutable;
using Build.Data.BindingGeneration.Models;
using Build.Targets.GenerateBindings.Model;
using Build.Targets.GenerateBindings.ModelBuilding.Declarations;
using Build.Targets.GenerateBindings.ModelBuilding.Types;
using Build.Tests.Fixtures;
using CppAst;

namespace Build.Tests.Unit.Targets.GenerateBindings.ModelBuilding.Declarations;

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
