using System.Runtime.InteropServices;
using Build.Data.BindingGeneration.Models;
using Build.Targets.GenerateBindings.Emit;
using Build.Targets.GenerateBindings.Emit.PublicApi;
using Build.Targets.GenerateBindings.Model;
using Build.Tests.Fixtures;
using Build.Tests.Unit.Targets.GenerateBindings.Emit;

namespace Build.Tests.Unit.Targets.GenerateBindings.Emit.PublicApi;

public sealed class PublicApiEmitterTests
{
    [Test]
    public async Task Emit_Should_Produce_Structs_File_When_Model_Has_Structs()
    {
        var model = BindingModelData.ModelWithStructs();

        var structsFile = StructEmitter.Emit(model.Structs, Options());

        await Assert.That(structsFile.Content).Contains("namespace SDL2;");
        await Assert.That(structsFile.Content).DoesNotContain("namespace Janset.SDL2.Core;");
        await Assert.That(structsFile.Content).Contains("[StructLayout(LayoutKind.Sequential)]");
        await Assert.That(structsFile.Content).Contains("public unsafe partial struct SDL_CustomBytes");
        await Assert.That(structsFile.Content).Contains("public fixed byte data[16];");
        await Assert.That(structsFile.Content).Contains("public partial struct SDL_GameControllerButtonBind");
        await Assert.That(structsFile.Content).Contains("public SDL_GameControllerButtonBind_value @value;");
        await Assert.That(structsFile.Content).Contains("[StructLayout(LayoutKind.Explicit, Size = 8)]");
        await Assert.That(structsFile.Content).Contains("public partial struct SDL_GameControllerButtonBind_value");
        await Assert.That(structsFile.Content).Contains("[FieldOffset(0)]");
        await Assert.That(structsFile.Content).Contains("public int button;");
        await Assert.That(structsFile.Content).Contains("public int axis;");
    }

    [Test]
    public async Task Emit_Should_Mark_Struct_Unsafe_When_It_Has_Pointer_Fields()
    {
        var structure = new BindingStruct(
            Name: "SDL_PointerFields",
            Fields:
            [
                new BindingStructField("name", NativeTypeRef.Indirection(
                    NativeTypeRef.Primitive("signed char", "sbyte", NativeAbiShape.Of("sbyte")),
                    indirectionDepth: 1,
                    managedName: "sbyte*"), FieldOffset: null),
            ],
            Layout: LayoutKind.Sequential,
            ExplicitSize: null);

        var structsFile = StructEmitter.Emit([structure], Options());

        await Assert.That(structsFile.Content).Contains("public unsafe partial struct SDL_PointerFields");
        await Assert.That(structsFile.Content).Contains("public sbyte* name;");
    }

    [Test]
    public async Task Emit_Should_Use_TfmSafe_Wrapper_For_Fixed_Array_Elements_That_CSharp_Fixed_Buffers_Do_Not_Support()
    {
        var structure = new BindingStruct(
            Name: "SDL_ColorScheme",
            Fields:
            [
                new BindingStructField("colors", BindingGenerationFixture.NativePrimitive("SDL_MessageBoxColor", "SDL_MessageBoxColor"), FieldOffset: null, FixedBufferLength: 5),
            ],
            Layout: LayoutKind.Sequential,
            ExplicitSize: null);

        var structsFile = StructEmitter.Emit([structure], Options());

        await Assert.That(structsFile.Content).Contains("public SDL_ColorScheme_colors colors;");
        await Assert.That(structsFile.Content).Contains("[StructLayout(LayoutKind.Sequential)]");
        await Assert.That(structsFile.Content).Contains("public partial struct SDL_ColorScheme_colors");
        await Assert.That(structsFile.Content).Contains("public SDL_MessageBoxColor Element0;");
        await Assert.That(structsFile.Content).Contains("public SDL_MessageBoxColor Element4;");
        await Assert.That(structsFile.Content).DoesNotContain("using System.Runtime.CompilerServices;");
        await Assert.That(structsFile.Content).DoesNotContain("[InlineArray(5)]");
        await Assert.That(structsFile.Content).DoesNotContain("fixed SDL_MessageBoxColor colors[5];");
    }

    [Test]
    public async Task Emit_Should_Mark_FixedArray_Wrapper_Unsafe_When_ElementType_Is_Pointer()
    {
        var pointerType = NativeTypeRef.Indirection(
            NativeTypeRef.Primitive("signed char", "sbyte", NativeAbiShape.Of("sbyte")),
            indirectionDepth: 1,
            managedName: "sbyte*");
        var structure = new BindingStruct(
            Name: "SDL_PointerTable",
            Fields:
            [
                new BindingStructField("entries", pointerType, FieldOffset: null, FixedBufferLength: 2),
            ],
            Layout: LayoutKind.Sequential,
            ExplicitSize: null);

        var structsFile = StructEmitter.Emit([structure], Options());

        await Assert.That(structsFile.Content).Contains("public SDL_PointerTable_entries entries;");
        await Assert.That(structsFile.Content).Contains("public partial struct SDL_PointerTable\n");
        await Assert.That(structsFile.Content).DoesNotContain("public unsafe partial struct SDL_PointerTable\n");
        await Assert.That(structsFile.Content).Contains("public unsafe partial struct SDL_PointerTable_entries");
        await Assert.That(structsFile.Content).Contains("public sbyte* Element0;");
        await Assert.That(structsFile.Content).Contains("public sbyte* Element1;");
    }

    [Test]
    public async Task Emit_Should_Produce_Constants_File_When_Model_Has_Constants()
    {
        var constants = new[]
        {
            new BindingConstant("SDL_INIT_TIMER", BindingGenerationFixture.NativeUInt(), "0x00000001u", ConstantKind.Literal),
            new BindingConstant("SDL_INIT_EVERYTHING", BindingGenerationFixture.NativeUInt(), "SDL_INIT_TIMER | SDL_INIT_AUDIO", ConstantKind.Computed),
            new BindingConstant("SDL_HINT_RENDER_DRIVER", BindingGenerationFixture.NativePrimitive("ReadOnlySpan<byte>", "ReadOnlySpan<byte>"), "\"SDL_RENDER_DRIVER\"u8", ConstantKind.Literal),
        };

        var constantsFile = ConstantEmitter.Emit(constants, Options());

        await Assert.That(constantsFile.Content).Contains("using System;");
        await Assert.That(constantsFile.Content).Contains("namespace SDL2;");
        await Assert.That(constantsFile.Content).Contains("public static partial class SDL");
        await Assert.That(constantsFile.Content).Contains("public const uint SDL_INIT_TIMER = 0x00000001u;");
        await Assert.That(constantsFile.Content).Contains("public const uint SDL_INIT_EVERYTHING = SDL_INIT_TIMER | SDL_INIT_AUDIO;");
        await Assert.That(constantsFile.Content).Contains("public static ReadOnlySpan<byte> SDL_HINT_RENDER_DRIVER => \"SDL_RENDER_DRIVER\"u8;");
        await Assert.That(constantsFile.Content).DoesNotContain("public const string SDL_HINT_RENDER_DRIVER");
    }

    [Test]
    public async Task Emit_Should_Produce_Enums_File_When_Model_Has_Enums()
    {
        var enumerations = new[]
        {
            new BindingEnumeration(
                Name: "SDL_WindowFlags",
                UnderlyingType: BindingGenerationFixture.NativeUInt(),
                IsFlags: true,
                Members:
                [
                    new BindingEnumMember("SDL_WINDOW_FULLSCREEN", "0x00000001u"),
                    new BindingEnumMember("SDL_WINDOW_OPENGL", "0x00000002u"),
                    new BindingEnumMember("SDL_WINDOW_MOUSE_GRABBED", "0x00000100u"),
                    new BindingEnumMember("SDL_WINDOW_FULLSCREEN_DESKTOP", "SDL_WINDOW_FULLSCREEN | 0x00001000u"),
                    new BindingEnumMember("SDL_WINDOW_INPUT_GRABBED", "SDL_WINDOW_MOUSE_GRABBED"),
                ]),
        };

        var enumsFile = EnumEmitter.Emit(enumerations, Options());

        await Assert.That(enumsFile.Content).Contains("using System;");
        await Assert.That(enumsFile.Content).Contains("namespace SDL2;");
        await Assert.That(enumsFile.Content).Contains("[Flags]");
        await Assert.That(enumsFile.Content).Contains("public enum SDL_WindowFlags : uint");
        await Assert.That(enumsFile.Content).Contains("SDL_WINDOW_FULLSCREEN = 0x00000001u,");
        await Assert.That(enumsFile.Content).Contains("SDL_WINDOW_FULLSCREEN_DESKTOP = SDL_WINDOW_FULLSCREEN | 0x00001000u,");
        await Assert.That(enumsFile.Content).Contains("SDL_WINDOW_INPUT_GRABBED = SDL_WINDOW_MOUSE_GRABBED,");
    }

    private static BindingEmissionOptions Options() => new("SDL2", "SDL");
}
