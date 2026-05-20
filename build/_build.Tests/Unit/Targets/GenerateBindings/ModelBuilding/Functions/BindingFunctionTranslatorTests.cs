using Build.Data.BindingGeneration.Models;
using Build.Targets.GenerateBindings.Model;
using Build.Targets.GenerateBindings.ModelBuilding.Declarations;
using Build.Targets.GenerateBindings.ModelBuilding.Functions;
using Build.Targets.GenerateBindings.ModelBuilding.Types;
using Build.Tests.Fixtures;
using CppAst;

namespace Build.Tests.Unit.Targets.GenerateBindings.ModelBuilding.Functions;

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
