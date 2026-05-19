using Build.Tests.Fixtures;
using Build.Targets.GenerateBindings.Model;
using Build.Targets.GenerateBindings.Parsing;
using Build.Targets.GenerateBindings.Translation;
using CppAst;

namespace Build.Tests.Unit.Targets.GenerateBindings.Translation;

[LinuxOnly]
public sealed class SemanticHeaderFixtureTests
{
    [Test]
    public async Task Fixtures_Should_Parse_Opaque_And_Concrete_Struct_Shapes()
    {
        var compilation = ParseFixture("GenerateBindings/SemanticTypes/opaque-and-concrete-structs.h");

        var window = compilation.Classes.Single(c => c.Name == "SDL_Window");
        var texture = compilation.Classes.Single(c => c.Name == "SDL_Texture");

        await Assert.That(window.Fields).IsEmpty();
        await Assert.That(texture.Fields.Select(f => f.Name).ToArray())
            .IsEquivalentTo(["format", "w", "h", "refcount"]);
    }

    [Test]
    public async Task Fixtures_Should_Parse_Typedef_Chains_And_String_Function_Signatures()
    {
        var compilation = ParseFixture("GenerateBindings/SemanticTypes/typedefs-bool-and-strings.h");

        var sdlBool = compilation.Enums.Single(e => e.Name == "SDL_bool");
        await Assert.That(sdlBool.Items.Select(i => i.Name).ToArray())
            .IsEquivalentTo(["SDL_FALSE", "SDL_TRUE"]);

        var audioFormat = compilation.Typedefs.Single(t => t.Name == "SDL_AudioFormat");
        var elementTypedef = await AssertType<CppTypedef>(audioFormat.ElementType);
        await Assert.That(elementTypedef.Name).IsEqualTo("Uint16");

        var getError = compilation.Functions.Single(f => f.Name == "SDL_GetError");
        await AssertConstCharPointer(getError.ReturnType);

        var setHint = compilation.Functions.Single(f => f.Name == "SDL_SetHint");
        var nameParam = setHint.Parameters.Single(p => p.Name == "name");
        await AssertConstCharPointer(nameParam.Type);
        var valueParam = setHint.Parameters.Single(p => p.Name == "value");
        await AssertConstCharPointer(valueParam.Type);
    }

    [Test]
    public async Task Fixtures_Should_Parse_Callback_Array_And_Anonymous_Union_Shapes()
    {
        var compilation = ParseFixture("GenerateBindings/SemanticTypes/callbacks-arrays-and-unions.h");

        var callback = compilation.Typedefs.Single(t => t.Name == "SDL_AudioCallback");
        var callbackPointer = await AssertType<CppPointerType>(callback.ElementType);
        var callbackFunction = await AssertType<CppFunctionType>(callbackPointer.ElementType);
        var callbackReturn = await AssertType<CppPrimitiveType>(callbackFunction.ReturnType);
        await Assert.That(callbackReturn.Kind).IsEqualTo(CppPrimitiveKind.Void);
        await Assert.That(callbackFunction.Parameters.Select(p => p.Name).ToArray())
            .IsEquivalentTo(["userdata", "stream", "len"]);
        await AssertType<CppPointerType>(callbackFunction.Parameters.Single(p => p.Name == "userdata").Type);
        var stream = await AssertType<CppPointerType>(callbackFunction.Parameters.Single(p => p.Name == "stream").Type);
        var streamElement = await AssertType<CppTypedef>(stream.ElementType);
        await Assert.That(streamElement.Name).IsEqualTo("Uint8");
        var len = await AssertType<CppPrimitiveType>(callbackFunction.Parameters.Single(p => p.Name == "len").Type);
        await Assert.That(len.Kind).IsEqualTo(CppPrimitiveKind.Int);

        var sdlEvent = compilation.Classes.Single(c => c.Name == "SDL_Event");
        await Assert.That(sdlEvent.Fields.Single(f => f.Name == "padding").Type).IsTypeOf<CppArrayType>();
        var bind = compilation.Classes.Single(c => c.Name == "SDL_GameControllerButtonBind");
        await Assert.That(bind.Fields.Single(f => f.Name == "value").Type).IsTypeOf<CppClass>();
    }

    [Test]
    public async Task Fixtures_Should_Parse_Object_Like_And_Function_Like_Macros()
    {
        var compilation = ParseFixture("GenerateBindings/MacroConstants/macro-constants.h", parseMacros: true);

        var renderDriver = compilation.Macros.Single(m => m.Name == "SDL_HINT_RENDER_DRIVER");
        await Assert.That(renderDriver.Value).IsEqualTo("\"SDL_RENDER_DRIVER\"");
        await Assert.That(MacroParameters(renderDriver)).IsEmpty();

        var button = compilation.Macros.Single(m => m.Name == "SDL_BUTTON");
        await Assert.That(button.Parameters).IsEquivalentTo(["X"]);
        await Assert.That(button.Value).Contains("1u");
    }

    [Test]
    public async Task Fixtures_Should_Parse_Enum_And_Flag_Expression_Members()
    {
        var compilation = ParseFixture("GenerateBindings/SemanticTypes/enums-and-flags.h");

        var windowFlags = compilation.Enums.Single(e => e.Name == "SDL_WindowFlags");
        await Assert.That(windowFlags.Items.Select(i => i.Name).ToArray())
            .IsEquivalentTo(["SDL_WINDOW_FULLSCREEN", "SDL_WINDOW_OPENGL", "SDL_WINDOW_SHOWN", "SDL_WINDOW_FULLSCREEN_DESKTOP"]);
        var desktopFullscreen = windowFlags.Items.Single(i => i.Name == "SDL_WINDOW_FULLSCREEN_DESKTOP");
        await Assert.That(desktopFullscreen.Value).IsEqualTo(0x00001001L);

        var eventType = compilation.Enums.Single(e => e.Name == "SDL_EventType");
        await Assert.That(eventType.Items.Select(i => i.Name).ToArray())
            .IsEquivalentTo(["SDL_FIRSTEVENT", "SDL_QUIT"]);
        await Assert.That(eventType.Items.Single(i => i.Name == "SDL_QUIT").Value).IsEqualTo(0x100L);
    }

    [Test]
    public async Task Fixtures_Should_Parse_Macro_Expression_Surface()
    {
        var compilation = ParseFixture("GenerateBindings/MacroConstants/macro-expressions.h", parseMacros: true);

        var hapticRamp = compilation.Macros.Single(m => m.Name == "SDL_HAPTIC_RAMP");
        await Assert.That(MacroParameters(hapticRamp)).IsEmpty();
        await Assert.That(hapticRamp.Value).Contains("1u");
        await Assert.That(hapticRamp.Value).Contains("6");

        var button = compilation.Macros.Single(m => m.Name == "SDL_BUTTON");
        await Assert.That(button.Parameters).IsEquivalentTo(["X"]);

        var buttonMask = compilation.Macros.Single(m => m.Name == "SDL_BUTTON_LMASK");
        await Assert.That(MacroParameters(buttonMask)).IsEmpty();
        await Assert.That(buttonMask.Value).Contains("SDL_BUTTON");

        var assertLevel = compilation.Macros.Single(m => m.Name == "SDL_ASSERT_LEVEL");
        await Assert.That(assertLevel.Value).IsEqualTo("1");

        var nullWhileLoopCondition = compilation.Macros.Single(m => m.Name == "SDL_NULL_WHILE_LOOP_CONDITION");
        await Assert.That(nullWhileLoopCondition.Value).IsEqualTo("(0)");

        var printfFormat = compilation.Macros.Single(m => m.Name == "SDL_PRIs64");
        await Assert.That(printfFormat.Value).IsEqualTo("\"I64d\"");

        var cachelineSize = compilation.Macros.Single(m => m.Name == "SDL_CACHELINE_SIZE");
        await Assert.That(cachelineSize.Value).IsEqualTo("128");

        var revisionNumber = compilation.Macros.Single(m => m.Name == "SDL_REVISION_NUMBER");
        await Assert.That(revisionNumber.Value).IsEqualTo("0");
    }

    [Test]
    public async Task Fixtures_Should_Translate_Contextual_Macro_Expressions_From_CppAst_Parse()
    {
        var compilation = ParseFixture(
            "GenerateBindings/MacroConstants/macro-expressions.h",
            parseMacros: true,
            parseAsSdl2Header: true);
        var result = BindingConstantTranslator.Translate(
            [ParseResult("Neutral", compilation)],
            BindingGenerationFixture.Sdl2CoreConfig());

        await Assert.That(result.Constants.Single(c => c.Name == "SDL_BUTTON_LMASK").Value).IsEqualTo("1u");
        await Assert.That(result.Constants.Single(c => c.Name == "SDL_WINDOWPOS_UNDEFINED").Value).IsEqualTo("536805376u");
        await Assert.That(result.Report.Entries.Single(e => e.Name == "SDL_BUTTON").Disposition).IsEqualTo("helper-candidate");
        await Assert.That(result.Report.Entries.Single(e => e.Name == "SDL_WINDOWPOS_UNDEFINED_DISPLAY").Disposition).IsEqualTo("helper-candidate");
        await AssertSkipped(result, "SDL_ASSERT_LEVEL", "SDL C assertion build-time macro");
        await AssertSkipped(result, "SDL_NULL_WHILE_LOOP_CONDITION", "SDL C assertion helper macro");
        await AssertSkipped(result, "SDL_PRIs64", "C printf format macro");
        await AssertSkipped(result, "SDL_CACHELINE_SIZE", "C-only cache-line padding macro");
        await AssertSkipped(result, "SDL_REVISION_NUMBER", "obsolete SDL revision macro");
    }

    [Test]
    public async Task Fixtures_Should_Skip_Platform_Control_Macros()
    {
        var compilation = ParseFixture(
            "GenerateBindings/SemanticTypes/enums-and-platform-control-macros.h",
            parseMacros: true,
            parseAsSdl2Header: true);
        var result = BindingConstantTranslator.Translate(
            [ParseResult("Neutral", compilation)],
            BindingGenerationFixture.Sdl2CoreConfig());

        await Assert.That(result.Constants.Select(constant => constant.Name).ToArray())
            .DoesNotContain("SDL_WINAPI_FAMILY_PHONE");
        await AssertSkipped(result, "SDL_WINAPI_FAMILY_PHONE", "platform control macro");
    }

    [Test]
    public async Task Fixtures_Should_Translate_SDL2_Bool_As_Int_Backed_Enum()
    {
        var model = TranslateFixture("GenerateBindings/SemanticTypes/enums-and-platform-control-macros.h");

        var sdlBool = model.Enums.Single(enumeration => enumeration.Name == "SDL_bool");

        await Assert.That(sdlBool.UnderlyingType.ManagedName).IsEqualTo("int");
        await Assert.That(sdlBool.Members.Select(member => member.Name).ToArray())
            .IsEquivalentTo(["SDL_FALSE", "SDL_TRUE"]);
    }

    [Test]
    public async Task Fixtures_Should_Translate_Known_Bitmask_Enums_As_Flags()
    {
        var model = TranslateFixture("GenerateBindings/SemanticTypes/enums-and-platform-control-macros.h");

        await Assert.That(model.Enums.Single(enumeration => enumeration.Name == "SDL_Keymod").IsFlags).IsTrue();
        await Assert.That(model.Enums.Single(enumeration => enumeration.Name == "SDL_GLcontextFlag").IsFlags).IsTrue();
        await Assert.That(model.Enums.Single(enumeration => enumeration.Name == "SDL_RendererFlip").IsFlags).IsTrue();
    }

    [Test]
    public async Task Fixtures_Should_Translate_Opaque_Handle_Typedefs_Without_Tag_Leaks()
    {
        var model = TranslateFixture("GenerateBindings/SemanticTypes/opaque-handle-aliases.h");

        var handleNames = model.Handles.Select(handle => handle.Name).ToArray();

        await Assert.That(handleNames).IsEquivalentTo(["SDL_hid_device", "SDL_sem", "SDL_Window"]);
        await Assert.That(handleNames).DoesNotContain("SDL_hid_device_");
        await Assert.That(handleNames).DoesNotContain("SDL_semaphore");
    }

    [Test]
    public async Task Fixtures_Should_Translate_C_Long_And_Unsigned_Long_As_Platform_C_Integers()
    {
        var model = TranslateFixture("GenerateBindings/SemanticTypes/platform-c-integers-and-wide-strings.h");
        var functions = model.Views.Single().Functions;

        await Assert.That(functions.Single(function => function.Name == "SDL_lround").ReturnType.ManagedName)
            .IsEqualTo("CLong");
        await Assert.That(functions.Single(function => function.Name == "SDL_strtoul").ReturnType.ManagedName)
            .IsEqualTo("CULong");
        await Assert.That(functions.Single(function => function.Name == "SDL_ThreadID").ReturnType.ManagedName)
            .IsEqualTo("CULong");
    }

    [Test]
    public async Task Fixtures_Should_Translate_WChar_Pointers_As_Opaque_Pointers()
    {
        var model = TranslateFixture("GenerateBindings/SemanticTypes/platform-c-integers-and-wide-strings.h");

        var hidInfo = model.Structs.Single(structure => structure.Name == "SDL_hid_device_info");
        var serial = hidInfo.Fields.Single(field => field.Name == "serial_number");
        var manufacturer = hidInfo.Fields.Single(field => field.Name == "manufacturer_string");
        var getSerial = model.Views.Single().Functions.Single(function => function.Name == "SDL_hid_get_serial_number_string");

        await Assert.That(serial.Type.ManagedName).IsEqualTo("nint");
        await Assert.That(manufacturer.Type.ManagedName).IsEqualTo("nint");
        await Assert.That(getSerial.Parameters.Single(parameter => parameter.Name == "@string").Type.ManagedName).IsEqualTo("nint");
    }

    [Test]
    public async Task Fixtures_Should_Translate_Hid_Wide_String_Pointers_As_Opaque_Pointers_When_CppAst_Erases_WChar()
    {
        var model = TranslateFixture("GenerateBindings/SemanticTypes/hid-wide-string-pointers.h");

        var hidInfo = model.Structs.Single(structure => structure.Name == "SDL_hid_device_info");
        await Assert.That(hidInfo.Fields.Single(field => field.Name == "serial_number").Type.ManagedName).IsEqualTo("nint");
        await Assert.That(hidInfo.Fields.Single(field => field.Name == "manufacturer_string").Type.ManagedName).IsEqualTo("nint");
        await Assert.That(hidInfo.Fields.Single(field => field.Name == "product_string").Type.ManagedName).IsEqualTo("nint");

        var functions = model.Views.Single().Functions;
        await Assert.That(functions.Single(function => function.Name == "SDL_hid_open")
            .Parameters.Single(parameter => parameter.Name == "serial_number").Type.ManagedName).IsEqualTo("nint");
        await Assert.That(functions.Single(function => function.Name == "SDL_hid_get_serial_number_string")
            .Parameters.Single(parameter => parameter.Name == "@string").Type.ManagedName).IsEqualTo("nint");
    }

    [Test]
    public async Task Fixtures_Should_Quarantine_SDL_RWops_False_Full_Layout_As_Opaque_Handle()
    {
        var model = TranslateFixture("GenerateBindings/SemanticTypes/rwops-platform-conditioned-layout.h");

        await Assert.That(model.Structs.Select(structure => structure.Name).ToArray()).DoesNotContain("SDL_RWops");
        await Assert.That(model.Structs.Select(structure => structure.Name).ToArray()).DoesNotContain("SDL_RWops_hidden");
        await Assert.That(model.Handles.Select(handle => handle.Name).ToArray()).Contains("SDL_RWops");

        var rwFromMem = model.Views.Single().Functions.Single(function => function.Name == "SDL_RWFromMem");
        await Assert.That(rwFromMem.ReturnType.ManagedName).IsEqualTo("SDL_RWops");
    }

    private static CppAstParseResult ParseResult(string viewName, CppCompilation compilation) =>
        new(
            new PlatformParseView(
                Name: viewName,
                Kind: PlatformConditionKind.Neutral,
                SupportedOsPlatform: null,
                Defines: [],
                Undefines: []),
            [compilation]);

    private static BindingModel TranslateFixture(string fixturePath, bool parseMacros = false)
    {
        var compilation = ParseFixture(fixturePath, parseMacros, parseAsSdl2Header: true);
        return CppAstToBindingModel.Translate(
            [ParseResult("Neutral", compilation)],
            BindingGenerationFixture.Sdl2CoreConfig(),
            requiredFunctions: []);
    }

    private static CppCompilation ParseFixture(
        string fixturePath,
        bool parseMacros = false,
        bool parseAsSdl2Header = false)
    {
        var directory = Path.Combine(Path.GetTempPath(), "janset-semantic-fixtures", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var headerDirectory = parseAsSdl2Header
            ? Path.Combine(directory, "include", "SDL2")
            : directory;
        Directory.CreateDirectory(headerDirectory);
        var header = Path.Combine(headerDirectory, Path.GetFileName(fixturePath));
        try
        {
            File.WriteAllText(header, FixtureLoader.Load(fixturePath));

            var options = new CppParserOptions
            {
                ParserKind = CppParserKind.C,
                TargetSystem = "linux",
                ParseMacros = parseMacros,
            };

            var compilation = CppParser.ParseFile(header, options);
            if (compilation.HasErrors)
            {
                throw new InvalidOperationException(compilation.Diagnostics.ToString());
            }

            return compilation;
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task<T> AssertType<T>(CppType type)
        where T : CppType
    {
        await Assert.That(type).IsTypeOf<T>();
        return (T)type;
    }

    private static async Task AssertConstCharPointer(CppType type)
    {
        var pointer = await AssertType<CppPointerType>(type);
        var qualified = await AssertType<CppQualifiedType>(pointer.ElementType);
        await Assert.That(qualified.Qualifier).IsEqualTo(CppTypeQualifier.Const);
        var element = await AssertType<CppPrimitiveType>(qualified.ElementType);
        await Assert.That(element.Kind).IsEqualTo(CppPrimitiveKind.Char);
    }

    private static List<string> MacroParameters(CppMacro macro) =>
        macro.Parameters is null ? [] : macro.Parameters;

    private static async Task AssertSkipped(BindingConstantTranslationResult result, string name, string reason)
    {
        await Assert.That(result.Constants.Any(c => c.Name == name)).IsFalse();
        var entry = result.Report.Entries.Single(e => e.Name == name);
        await Assert.That(entry.Disposition).IsEqualTo("skipped");
        await Assert.That(entry.Reason).IsEqualTo(reason);
    }
}
