using System.Collections.Immutable;
using Build.Data.BindingGeneration.Models;
using Build.Targets.GenerateBindings.Model;
using Build.Targets.GenerateBindings.Translation;
using Build.Tests.Fixtures;
using CppAst;

namespace Build.Tests.Unit.Targets.GenerateBindings.Translation;

public sealed class NativeTypeClassifierTests
{
    [Test]
    public async Task Classify_Should_Distinguish_Opaque_Handle_From_Concrete_Struct()
    {
        var classifier = CreateClassifier();
        var opaqueWindow = new CppClass("SDL_Window")
        {
            ClassKind = CppClassKind.Struct,
            IsDefinition = false,
        };
        var texture = new CppClass("SDL_Texture")
        {
            ClassKind = CppClassKind.Struct,
            IsDefinition = true,
            SizeOf = 16,
        };
        texture.Fields.Add(new CppField(CppPrimitiveType.UnsignedInt, "format"));

        var window = classifier.Classify(opaqueWindow, sourceHeader: "SDL_video.h");
        var concrete = classifier.Classify(texture, sourceHeader: "SDL_render.h");

        await Assert.That(window.Kind).IsEqualTo(NativeTypeKind.OpaqueHandle);
        await Assert.That(window.ManagedName).IsEqualTo("SDL_Window");
        await Assert.That(concrete.Kind).IsEqualTo(NativeTypeKind.ConcreteStruct);
        await Assert.That(concrete.ManagedName).IsEqualTo("SDL_Texture");
    }

    [Test]
    public async Task Classify_Should_Map_SDL2_Bool_To_Int_Backed_Primitive()
    {
        var classifier = CreateClassifier();
        var type = new CppTypedef("SDL_bool", new CppEnum("SDL_bool"));

        var sut = classifier.Classify(type, sourceHeader: "SDL_stdinc.h");

        await Assert.That(sut.Kind).IsEqualTo(NativeTypeKind.ValueTypedef);
        await Assert.That(sut.ManagedName).IsEqualTo("int");
        await Assert.That(sut.AbiShape.StorageName).IsEqualTo("int");
    }

    [Test]
    public async Task Classify_Should_Preserve_C_Primitive_Native_Name_Separately_From_Managed_Name()
    {
        var classifier = CreateClassifier();

        var sut = classifier.Classify(CppPrimitiveType.UnsignedInt, sourceHeader: "SDL_stdinc.h");

        await Assert.That(sut.NativeName).IsEqualTo("unsigned int");
        await Assert.That(sut.ManagedName).IsEqualTo("uint");
    }

    [Test]
    public async Task Classify_Should_Classify_Const_Char_Pointer_As_Utf8Pointer()
    {
        var classifier = CreateClassifier();
        var pointer = new CppPointerType(new CppQualifiedType(CppTypeQualifier.Const, CppPrimitiveType.Char));

        var sut = classifier.Classify(pointer, sourceHeader: "SDL_video.h");

        await Assert.That(sut.Kind).IsEqualTo(NativeTypeKind.Utf8Pointer);
        await Assert.That(sut.ManagedName).IsEqualTo("byte*");
        await Assert.That(sut.PointerDepth).IsEqualTo(1);
        await Assert.That(sut.ElementType?.NativeName).IsEqualTo("char");
    }

    [Test]
    public async Task Classify_Should_Preserve_Opaque_Handle_Element_When_Class_Pointer()
    {
        var classifier = CreateClassifier();
        var opaqueWindow = new CppClass("SDL_Window")
        {
            ClassKind = CppClassKind.Struct,
            IsDefinition = false,
        };
        var pointer = new CppPointerType(opaqueWindow);

        var sut = classifier.Classify(pointer, sourceHeader: "SDL_video.h");

        await Assert.That(sut.Kind).IsEqualTo(NativeTypeKind.TypedPointer);
        await Assert.That(sut.ManagedName).IsEqualTo("SDL_Window");
        await Assert.That(sut.ElementType?.Kind).IsEqualTo(NativeTypeKind.OpaqueHandle);
        await Assert.That(sut.ElementType?.ManagedName).IsEqualTo("SDL_Window");
    }

    [Test]
    public async Task Classify_Should_Preserve_MultiLevel_Opaque_Handle_Pointer()
    {
        var classifier = CreateClassifier();
        var privateWindow = new CppClass("__SDL_PrivateWindow")
        {
            ClassKind = CppClassKind.Struct,
            IsDefinition = false,
        };
        var typedef = new CppTypedef("SDL_Window", privateWindow);
        var pointer = new CppPointerType(new CppPointerType(typedef));

        var sut = classifier.Classify(pointer, sourceHeader: "SDL_render.h");

        await Assert.That(sut.Kind).IsEqualTo(NativeTypeKind.TypedPointer);
        await Assert.That(sut.ManagedName).IsEqualTo("SDL_Window*");
        await Assert.That(sut.PointerDepth).IsEqualTo(2);
        await Assert.That(sut.ElementType?.Kind).IsEqualTo(NativeTypeKind.OpaqueHandle);
        await Assert.That(sut.ElementType?.ManagedName).IsEqualTo("SDL_Window");
    }

    [Test]
    public async Task Classify_Should_Preserve_Opaque_Handle_Element_When_Typedef_Pointer()
    {
        var classifier = CreateClassifier();
        var privateWindow = new CppClass("__SDL_PrivateWindow")
        {
            ClassKind = CppClassKind.Struct,
            IsDefinition = false,
        };
        var typedef = new CppTypedef("SDL_Window", privateWindow);
        var pointer = new CppPointerType(typedef);

        var sut = classifier.Classify(pointer, sourceHeader: "SDL_video.h");

        await Assert.That(sut.Kind).IsEqualTo(NativeTypeKind.TypedPointer);
        await Assert.That(sut.ManagedName).IsEqualTo("SDL_Window");
        await Assert.That(sut.ElementType?.Kind).IsEqualTo(NativeTypeKind.OpaqueHandle);
        await Assert.That(sut.ElementType?.ManagedName).IsEqualTo("SDL_Window");
    }

    [Test]
    public async Task Classify_Should_Map_C_Runtime_Internal_Pointer_To_IntPtr()
    {
        var classifier = CreateClassifier();
        var filePointer = new CppPointerType(new CppClass("_IO_FILE"));

        var sut = classifier.Classify(filePointer, sourceHeader: "SDL_rwops.h");

        await Assert.That(sut.Kind).IsEqualTo(NativeTypeKind.ExternalOpaque);
        await Assert.That(sut.ManagedName).IsEqualTo("IntPtr");
        await Assert.That(sut.PointerDepth).IsEqualTo(1);
    }

    [Test]
    public async Task Classify_Should_Map_Sdl_Private_Opaque_Pointer_Typedef_To_IntPtr()
    {
        var classifier = CreateClassifier();
        var iconv = new CppTypedef("SDL_iconv_t", new CppPointerType(new CppClass("_SDL_iconv_t")));

        var sut = classifier.Classify(iconv, sourceHeader: "SDL_stdinc.h");

        await Assert.That(sut.Kind).IsEqualTo(NativeTypeKind.ExternalOpaque);
        await Assert.That(sut.NativeName).IsEqualTo("SDL_iconv_t");
        await Assert.That(sut.ManagedName).IsEqualTo("IntPtr");
    }

    [Test]
    public async Task Classify_Should_Map_Windows_Com_Device_Pointers_To_IntPtr()
    {
        var classifier = CreateClassifier();

        var d3d11 = classifier.Classify(new CppPointerType(new CppClass("ID3D11Device")), sourceHeader: "SDL_system.h");
        var d3d12 = classifier.Classify(new CppPointerType(new CppClass("ID3D12Device")), sourceHeader: "SDL_system.h");
        var d3d9 = classifier.Classify(new CppPointerType(new CppClass("IDirect3DDevice9")), sourceHeader: "SDL_system.h");

        await Assert.That(new[] { d3d11.ManagedName, d3d12.ManagedName, d3d9.ManagedName })
            .IsEquivalentTo(["IntPtr", "IntPtr", "IntPtr"]);
        await Assert.That(new[] { d3d11.Kind, d3d12.Kind, d3d9.Kind })
            .IsEquivalentTo([NativeTypeKind.ExternalOpaque, NativeTypeKind.ExternalOpaque, NativeTypeKind.ExternalOpaque]);
    }

    [Test]
    public async Task Classify_Should_Classify_Pointer_Typedef_As_Pointer_Typedef()
    {
        var classifier = CreateClassifier();
        var typedef = new CppTypedef("SDL_GLContext", new CppPointerType(CppPrimitiveType.Void));

        var sut = classifier.Classify(typedef, sourceHeader: "SDL_video.h");

        await Assert.That(sut.NativeName).IsEqualTo("SDL_GLContext");
        await Assert.That(sut.Kind).IsEqualTo(NativeTypeKind.VoidPointer);
        await Assert.That(sut.ManagedName).IsEqualTo("nint");
        await Assert.That(sut.PointerDepth).IsEqualTo(1);
    }

    [Test]
    public async Task Classify_Should_Classify_Pointer_To_Pointer_Typedef()
    {
        var classifier = CreateClassifier();
        var glContext = new CppTypedef("SDL_GLContext", new CppPointerType(CppPrimitiveType.Void));
        var utf8Alias = new CppTypedef("SDL_Utf8Alias", new CppPointerType(CppPrimitiveType.Char));

        var glContextPointer = classifier.Classify(new CppPointerType(glContext), sourceHeader: "SDL_video.h");
        var utf8AliasPointer = classifier.Classify(new CppPointerType(utf8Alias), sourceHeader: "SDL_test.h");

        await Assert.That(glContextPointer.Kind).IsEqualTo(NativeTypeKind.TypedPointer);
        await Assert.That(glContextPointer.ManagedName).IsEqualTo("nint*");
        await Assert.That(glContextPointer.PointerDepth).IsEqualTo(2);
        await Assert.That(glContextPointer.ElementType?.NativeName).IsEqualTo("void");
        await Assert.That(utf8AliasPointer.Kind).IsEqualTo(NativeTypeKind.TypedPointer);
        await Assert.That(utf8AliasPointer.ManagedName).IsEqualTo("byte**");
        await Assert.That(utf8AliasPointer.PointerDepth).IsEqualTo(2);
        await Assert.That(utf8AliasPointer.ElementType?.NativeName).IsEqualTo("char");
    }

    [Test]
    public async Task Classify_Should_Preserve_MultiLevel_Void_And_Char_Pointers()
    {
        var classifier = CreateClassifier();
        var voidPointer = new CppPointerType(new CppPointerType(CppPrimitiveType.Void));
        var charPointer = new CppPointerType(new CppPointerType(CppPrimitiveType.Char));

        var voidRef = classifier.Classify(voidPointer, sourceHeader: "SDL_test.h");
        var charRef = classifier.Classify(charPointer, sourceHeader: "SDL_test.h");

        await Assert.That(voidRef.Kind).IsEqualTo(NativeTypeKind.TypedPointer);
        await Assert.That(voidRef.ManagedName).IsEqualTo("nint*");
        await Assert.That(voidRef.PointerDepth).IsEqualTo(2);
        await Assert.That(voidRef.ElementType?.NativeName).IsEqualTo("void");
        await Assert.That(charRef.Kind).IsEqualTo(NativeTypeKind.TypedPointer);
        await Assert.That(charRef.ManagedName).IsEqualTo("byte**");
        await Assert.That(charRef.PointerDepth).IsEqualTo(2);
        await Assert.That(charRef.ElementType?.NativeName).IsEqualTo("char");
    }

    [Test]
    public async Task Classify_Should_Preserve_Outer_Typedef_Identity_Through_Typedef_Chain()
    {
        var classifier = CreateClassifier();
        var uint16 = new CppTypedef("Uint16", CppPrimitiveType.UnsignedShort);
        var type = new CppTypedef("SDL_AudioFormat", uint16);

        var sut = classifier.Classify(type, sourceHeader: "SDL_audio.h");

        await Assert.That(sut.NativeName).IsEqualTo("SDL_AudioFormat");
        await Assert.That(sut.ManagedName).IsEqualTo("ushort");
        await Assert.That(sut.Kind).IsEqualTo(NativeTypeKind.ValueTypedef);
        await Assert.That(sut.OwningFamilyId).IsEqualTo("sdl2-core");
    }

    [Test]
    public async Task Classify_Should_Use_Typedef_Name_For_Ownership_When_Class_Name_Is_Not_Owned()
    {
        var classifier = CreateClassifier();
        var privateWindow = new CppClass("__SDL_PrivateWindow")
        {
            ClassKind = CppClassKind.Struct,
            IsDefinition = false,
        };
        var type = new CppTypedef("SDL_Window", privateWindow);

        var sut = classifier.Classify(type, sourceHeader: "SDL_video.h");

        await Assert.That(sut.NativeName).IsEqualTo("SDL_Window");
        await Assert.That(sut.Kind).IsEqualTo(NativeTypeKind.OpaqueHandle);
        await Assert.That(sut.OwningFamilyId).IsEqualTo("sdl2-core");
    }

    [Test]
    public async Task Classify_Should_Assign_Ownership_To_Owned_Enums()
    {
        var classifier = CreateClassifier();
        var enumeration = new CppEnum("SDL_EventType");

        var sut = classifier.Classify(enumeration, sourceHeader: "SDL_events.h");

        await Assert.That(sut.Kind).IsEqualTo(NativeTypeKind.Enum);
        await Assert.That(sut.OwningFamilyId).IsEqualTo("sdl2-core");
    }

    [Test]
    public async Task Classify_Should_Mark_Deferred_Declarations_By_Name()
    {
        var config = BindingGenerationFixture.Sdl2CoreConfig() with
        {
            DeferredDeclarations = ImmutableDictionary<string, DeferredDeclarationConfig>.Empty.Add(
                "SDL_SysWMinfo",
                new DeferredDeclarationConfig
                {
                    Category = "deferred-to-stage-2",
                    Reason = "Platform-specific union.",
                }),
        };
        var classifier = CreateClassifier(config);
        var sysWmInfo = new CppClass("SDL_SysWMinfo")
        {
            ClassKind = CppClassKind.Struct,
            IsDefinition = true,
            SizeOf = 64,
        };

        var sut = classifier.Classify(sysWmInfo, sourceHeader: "SDL_syswm.h");

        await Assert.That(sut.Kind).IsEqualTo(NativeTypeKind.Deferred);
        await Assert.That(sut.NativeName).IsEqualTo("SDL_SysWMinfo");
        await Assert.That(sut.ManagedName).IsEqualTo("nint");
    }

    [Test]
    public async Task Classify_Should_Report_Unsupported_Type_With_Normalized_Warning_Diagnostic()
    {
        var classifier = CreateClassifier();

        var sut = classifier.Classify(new CppArrayType(CppPrimitiveType.Int, 4), sourceHeader: "SDL_test.h");

        await Assert.That(sut.Kind).IsEqualTo(NativeTypeKind.Unsupported);
        await Assert.That(sut.Diagnostics.Count).IsEqualTo(1);
        await Assert.That(sut.Diagnostics[0].Severity).IsEqualTo(NativeTypeDiagnosticSeverity.Warning);
    }

    [Test]
    public async Task Classify_Should_Map_Callback_Typedef_References_To_Function_Pointer_Storage()
    {
        var classifier = CreateClassifier();
        var functionType = new CppFunctionType(CppPrimitiveType.Int);
        functionType.Parameters.Add(new CppParameter(new CppPointerType(CppPrimitiveType.Void), "userdata"));
        var typedef = new CppTypedef("SDL_EventFilter", new CppPointerType(functionType));

        var sut = classifier.Classify(typedef, sourceHeader: "SDL_events.h");

        await Assert.That(sut.Kind).IsEqualTo(NativeTypeKind.FunctionPointer);
        await Assert.That(sut.NativeName).IsEqualTo("SDL_EventFilter");
        await Assert.That(sut.ManagedName).IsEqualTo("IntPtr");
        await Assert.That(sut.PointerDepth).IsEqualTo(1);
    }

    [Test]
    public async Task Classify_Should_Map_Direct_Function_Pointer_Fields_To_IntPtr()
    {
        var classifier = CreateClassifier();
        var functionType = new CppFunctionType(CppPrimitiveType.Int);
        functionType.Parameters.Add(new CppParameter(new CppPointerType(CppPrimitiveType.Void), "userdata"));
        var fieldType = new CppPointerType(functionType);

        var sut = classifier.Classify(fieldType, sourceHeader: "SDL_rwops.h");

        await Assert.That(sut.Kind).IsEqualTo(NativeTypeKind.FunctionPointer);
        await Assert.That(sut.NativeName).Contains("(*)");
        await Assert.That(sut.ManagedName).IsEqualTo("IntPtr");
    }

    [Test]
    public async Task Classify_Should_Map_Pointer_To_Callback_Typedef_As_IntPtr_Pointer()
    {
        var classifier = CreateClassifier();
        var functionType = new CppFunctionType(CppPrimitiveType.Int);
        functionType.Parameters.Add(new CppParameter(new CppPointerType(CppPrimitiveType.Void), "userdata"));
        var callback = new CppTypedef("SDL_EventFilter", new CppPointerType(functionType));
        var callbackPointer = new CppPointerType(callback);

        var sut = classifier.Classify(callbackPointer, sourceHeader: "SDL_events.h");

        await Assert.That(sut.Kind).IsEqualTo(NativeTypeKind.FunctionPointer);
        await Assert.That(sut.NativeName).IsEqualTo("SDL_EventFilter");
        await Assert.That(sut.ManagedName).IsEqualTo("IntPtr*");
        await Assert.That(sut.PointerDepth).IsEqualTo(2);
    }

    [Test]
    public async Task Classify_Should_Map_Array_Parameters_As_Pointers()
    {
        var classifier = CreateClassifier();
        var argv = new CppArrayType(new CppPointerType(CppPrimitiveType.Char), -1);

        var sut = classifier.Classify(argv, sourceHeader: "SDL_main.h");

        await Assert.That(sut.Kind).IsEqualTo(NativeTypeKind.TypedPointer);
        await Assert.That(sut.NativeName).IsEqualTo("char");
        await Assert.That(sut.ManagedName).IsEqualTo("byte**");
        await Assert.That(sut.PointerDepth).IsEqualTo(2);
    }

    private static NativeTypeClassifier CreateClassifier(BindingGenerationConfig? config = null)
    {
        config ??= BindingGenerationFixture.Sdl2CoreConfig();
        var context = NativeTypeClassificationContext.FromConfig(config);
        return new NativeTypeClassifier(context);
    }
}
