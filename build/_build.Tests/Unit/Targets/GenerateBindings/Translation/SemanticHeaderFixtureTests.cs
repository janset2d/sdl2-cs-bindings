using Build.Tests.Fixtures;
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

    private static CppCompilation ParseFixture(string fixturePath)
    {
        var directory = Path.Combine(Path.GetTempPath(), "janset-semantic-fixtures", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var header = Path.Combine(directory, Path.GetFileName(fixturePath));
        try
        {
            File.WriteAllText(header, FixtureLoader.Load(fixturePath));

            var options = new CppParserOptions
            {
                ParserKind = CppParserKind.C,
                TargetSystem = "linux",
                ParseMacros = false,
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
}
