using System.Runtime.InteropServices;
using Build.Data.BindingGeneration.Models;
using Build.Targets.GenerateBindings.Model;
using Build.Tests.Fixtures;

namespace Build.Tests.Unit.Targets.GenerateBindings.Model;

/// <summary>
/// BindingModel extension records: record-shape + value-equality + the
/// <see cref="BindingTypeRef.Of(string)"/> bridge heuristic. This suite pins the
/// in-memory shape so a future record-redesign that breaks equality contracts or
/// ctor positional ordering surfaces as a test failure.
/// </summary>
public sealed class BindingTypeRefTests
{
    [Test]
    public async Task Of_Should_Set_IsPointer_True_For_Names_Ending_With_Star()
    {
        var typeRef = BindingTypeRef.Of("SDL_Surface*");
        await Assert.That(typeRef.ManagedName).IsEqualTo("SDL_Surface*");
        await Assert.That(typeRef.IsPointer).IsTrue();
    }

    [Test]
    public async Task Of_Should_Set_IsPointer_False_For_Primitive_Names()
    {
        var typeRef = BindingTypeRef.Of("int");
        await Assert.That(typeRef.IsPointer).IsFalse();
    }

    [Test]
    public async Task Of_Should_Set_IsPointer_False_For_IntPtr()
    {
        // IntPtr models a typed opaque handle, not a pointer in the pointer-arithmetic
        // sense; the heuristic (EndsWith '*') correctly returns false. Translator
        // structural inspection can tighten this further with CppType info.
        var typeRef = BindingTypeRef.Of("IntPtr");
        await Assert.That(typeRef.IsPointer).IsFalse();
    }

    [Test]
    public async Task Of_Should_Default_OwningFamilyId_And_IsOpaqueHandle_For_Stage1_Bridge()
    {
        var typeRef = BindingTypeRef.Of("byte*");
        await Assert.That(typeRef.OwningFamilyId).IsNull();
        await Assert.That(typeRef.IsOpaqueHandle).IsFalse();
    }

    [Test]
    public async Task Records_With_Equal_Fields_Should_Compare_Equal()
    {
        var a = new BindingTypeRef("int", null, false, false);
        var b = new BindingTypeRef("int", null, false, false);
        await Assert.That(a).IsEqualTo(b);
    }

    [Test]
    public async Task Records_With_Different_OwningFamilyId_Should_Not_Compare_Equal()
    {
        var coreOwned = new BindingTypeRef("SDL_Surface*", OwningFamilyId: "sdl2-core", IsPointer: true, IsOpaqueHandle: false);
        var unowned = new BindingTypeRef("SDL_Surface*", OwningFamilyId: null, IsPointer: true, IsOpaqueHandle: false);
        await Assert.That(coreOwned).IsNotEqualTo(unowned);
    }
}

public sealed class BindingStructTests
{
    [Test]
    public async Task Sequential_Layout_Should_Leave_FieldOffset_Null()
    {
        var field = new BindingStructField("w", BindingGenerationFixture.NativeInt(), FieldOffset: null);
        await Assert.That(field.FieldOffset).IsNull();
    }

    [Test]
    public async Task Fixed_Buffer_Field_Should_Carry_Array_Length()
    {
        var field = new BindingStructField("data", BindingGenerationFixture.NativePrimitive("unsigned char", "byte"), FieldOffset: null, FixedBufferLength: 16);

        await Assert.That(field.FixedBufferLength).IsEqualTo(16);
    }

    [Test]
    public async Task Explicit_Layout_Should_Carry_ExplicitSize_And_FieldOffset()
    {
        // SDL_SysWMinfo-style typed union: Layout=Explicit + ExplicitSize=64 on the
        // struct, FieldOffset=0 on the union fields.
        var field = new BindingStructField("union_data", BindingGenerationFixture.NativePrimitive("void*", "IntPtr"), FieldOffset: 0);
        var sut = new BindingStruct(
            Name: "SDL_SysWMinfo",
            Fields: [field],
            Layout: LayoutKind.Explicit,
            ExplicitSize: 64);

        await Assert.That(sut.Layout).IsEqualTo(LayoutKind.Explicit);
        await Assert.That(sut.ExplicitSize).IsEqualTo(64);
        await Assert.That(sut.Fields[0].FieldOffset).IsEqualTo(0);
    }
}

public sealed class BindingEnumerationTests
{
    [Test]
    public async Task IsFlags_True_Should_Survive_Into_Record()
    {
        var sut = new BindingEnumeration(
            Name: "SDL_WindowFlags",
            UnderlyingType: BindingGenerationFixture.NativeUInt(),
            IsFlags: true,
            Members: [new BindingEnumMember("SDL_WINDOW_FULLSCREEN", "0x00000001")]);

        await Assert.That(sut.IsFlags).IsTrue();
        await Assert.That(sut.Members[0].Value).IsEqualTo("0x00000001");
    }

    [Test]
    public async Task IsFlags_False_Maps_To_Non_Flags_Enum_Emit()
    {
        var sut = new BindingEnumeration(
            Name: "SDL_KeyState",
            UnderlyingType: BindingGenerationFixture.NativePrimitive("unsigned char", "byte"),
            IsFlags: false,
            Members:
            [
                new BindingEnumMember("SDL_RELEASED", "0"),
                new BindingEnumMember("SDL_PRESSED", "1"),
            ]);

        await Assert.That(sut.IsFlags).IsFalse();
        await Assert.That(sut.Members.Count).IsEqualTo(2);
    }
}

public sealed class BindingConstantTests
{
    [Test]
    public async Task Literal_Kind_Should_Emit_Through_Const_Path()
    {
        // ConstantEmitter keeps Kind as macro-shape metadata.
        // Numeric Literal and Computed macro expressions can both emit through
        // the const path when the RHS is valid C# compile-time syntax.
        var sut = new BindingConstant(
            Name: "SDL_INIT_TIMER",
            Type: BindingGenerationFixture.NativeUInt(),
            Value: "0x00000001u",
            Kind: ConstantKind.Literal);

        await Assert.That(sut.Kind).IsEqualTo(ConstantKind.Literal);
    }

    [Test]
    public async Task Computed_Kind_Should_Record_Macro_Expression_Shape()
    {
        var sut = new BindingConstant(
            Name: "SDL_INIT_EVERYTHING",
            Type: BindingGenerationFixture.NativeUInt(),
            Value: "SDL_INIT_TIMER | SDL_INIT_AUDIO",
            Kind: ConstantKind.Computed);

        await Assert.That(sut.Kind).IsEqualTo(ConstantKind.Computed);
    }

    [Test]
    public async Task Same_Name_Different_Kind_Should_Compare_Unequal()
    {
        var literal = new BindingConstant("X", BindingGenerationFixture.NativeUInt(), "1u", ConstantKind.Literal);
        var computed = new BindingConstant("X", BindingGenerationFixture.NativeUInt(), "1u", ConstantKind.Computed);
        await Assert.That(literal).IsNotEqualTo(computed);
    }
}

public sealed class BindingHandleTests
{
    [Test]
    public async Task Records_With_Same_Name_Should_Compare_Equal()
    {
        var type = NativeTypeRef.OpaqueHandle("SDL_Window", "SDL_Window", null, null);
        var a = new BindingHandle("SDL_Window", type);
        var b = new BindingHandle("SDL_Window", type);
        await Assert.That(a).IsEqualTo(b);
    }
}

public sealed class BindingModelRecordsTests
{
    [Test]
    public async Task BindingParameter_Should_Carry_Semantic_Native_Type()
    {
        var type = NativeTypeRef.OpaqueHandle("SDL_Window", "SDL_Window", "sdl2-core", "SDL_video.h");
        var sut = new BindingParameter(type, "window");

        await Assert.That(sut.Type.Kind).IsEqualTo(NativeTypeKind.OpaqueHandle);
        await Assert.That(sut.Type.ManagedName).IsEqualTo("SDL_Window");
        await Assert.That(sut.Name).IsEqualTo("window");
    }
}

public sealed class BindingCallbackTests
{
    [Test]
    public async Task Records_Should_Carry_Signature_For_Phase3E_Emit()
    {
        var eventPtrType = NativeTypeRef.Indirection(
            NativeTypeRef.Primitive("SDL_Event", "SDL_Event", NativeAbiShape.Of("SDL_Event")),
            indirectionDepth: 1,
            managedName: "SDL_Event*");
        var sut = new BindingCallback(
            Name: "SDL_EventFilter",
            ReturnType: BindingGenerationFixture.NativeInt(),
            Parameters:
            [
                new BindingParameter(BindingGenerationFixture.NativePrimitive("void*", "IntPtr"), "userdata"),
                new BindingParameter(eventPtrType, "event"),
            ]);

        await Assert.That(sut.Parameters.Count).IsEqualTo(2);
        await Assert.That(sut.Parameters[1].Type.PointerDepth).IsGreaterThan(0);
    }
}

public sealed class BindingModelExtendedShapeTests
{
    [Test]
    public async Task Convenience_Single_Arg_Ctor_Should_Default_New_Categories_To_Empty()
    {
        // Stage 1 translator + test fixtures construct BindingModel(views) without
        // the 5 new collections; the convenience ctor must pin empty defaults so
        // emitters / validators see deterministic empty state.
        var sut = new BindingModel(Views: []);

        await Assert.That(sut.Structs).IsEmpty();
        await Assert.That(sut.Enums).IsEmpty();
        await Assert.That(sut.Constants).IsEmpty();
        await Assert.That(sut.Handles).IsEmpty();
        await Assert.That(sut.Callbacks).IsEmpty();
    }

    [Test]
    public async Task Full_Ctor_Should_Surface_All_Five_New_Collections()
    {
        var handleType = NativeTypeRef.OpaqueHandle("SDL_Window", "SDL_Window", null, null);
        var sut = new BindingModel(
            Views: [],
            Structs: [new BindingStruct("SDL_Rect", [], LayoutKind.Sequential, null)],
            Enums: [new BindingEnumeration(
                Name: "SDL_KeyState",
                UnderlyingType: BindingGenerationFixture.NativePrimitive("unsigned char", "byte"),
                IsFlags: false,
                Members: [])],
            Constants: [new BindingConstant("SDL_INIT_TIMER", BindingGenerationFixture.NativeUInt(), "0x1u", ConstantKind.Literal)],
            Handles: [new BindingHandle("SDL_Window", handleType)],
            Callbacks: [new BindingCallback("SDL_EventFilter", BindingGenerationFixture.NativeInt(), [])]);

        await Assert.That(sut.Structs.Count).IsEqualTo(1);
        await Assert.That(sut.Enums.Count).IsEqualTo(1);
        await Assert.That(sut.Constants.Count).IsEqualTo(1);
        await Assert.That(sut.Handles.Count).IsEqualTo(1);
        await Assert.That(sut.Callbacks.Count).IsEqualTo(1);
    }
}
