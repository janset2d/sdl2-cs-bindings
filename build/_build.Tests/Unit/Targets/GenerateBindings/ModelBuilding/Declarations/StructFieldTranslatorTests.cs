using System.Runtime.InteropServices;
using Build.Data.BindingGeneration.Models;
using Build.Targets.GenerateBindings.ModelBuilding.Declarations;
using Build.Targets.GenerateBindings.ModelBuilding.Types;
using Build.Tests.Fixtures;
using CppAst;

namespace Build.Tests.Unit.Targets.GenerateBindings.ModelBuilding.Declarations;

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
