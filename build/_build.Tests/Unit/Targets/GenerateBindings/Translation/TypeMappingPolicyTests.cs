using Build.Targets.GenerateBindings.Model;
using Build.Targets.GenerateBindings.Translation;
using CppAst;

namespace Build.Tests.Unit.Targets.GenerateBindings.Translation;

// Targets TypeMappingPolicy.Map / MapPrimitive / MapTypedef / MapPointer +
// SafeIdentifier. These tests pin wire-format behavior that symbol-name
// validation cannot see: SDL primitive typedefs must not collapse to IntPtr,
// and platform-sensitive C integers must not collapse to fixed-width aliases.
// Assertions on BindingTypeRef.ManagedName preserve the string-flow contract
// consumed by older adapters that still use this policy directly.
//
// CppPrimitiveType has no public constructor — its instances are static
// singletons (CppPrimitiveType.Int, CppPrimitiveType.UnsignedShort, etc.).
// Tests reference those.
public sealed class TypeMappingPolicyTests
{
    // ─── Typedef chains resolve before SDL_*-prefix fallback ───

    [Test]
    public async Task Map_Should_Resolve_SDL_AudioFormat_Typedef_To_Underlying_Ushort()
    {
        // typedef Uint16 SDL_AudioFormat — Uint16 is a known explicit-width SDL2
        // typedef that maps to `ushort`. SDL_AudioFormat then resolves through
        // its element (Uint16) and inherits `ushort`. Previously the SDL_*
        // prefix fallback fired first and emitted IntPtr (8 bytes).
        var uint16Typedef = new CppTypedef("Uint16", CppPrimitiveType.UnsignedShort);
        var sdlAudioFormat = new CppTypedef("SDL_AudioFormat", uint16Typedef);

        await Assert.That(TypeMappingPolicy.Map(sdlAudioFormat).ManagedName).IsEqualTo("ushort");
    }

    [Test]
    public async Task Map_Should_Resolve_SDL_SpinLock_Typedef_To_Underlying_Int()
    {
        var sdlSpinLock = new CppTypedef("SDL_SpinLock", CppPrimitiveType.Int);

        await Assert.That(TypeMappingPolicy.Map(sdlSpinLock).ManagedName).IsEqualTo("int");
    }

    [Test]
    public async Task Map_Should_Emit_Int_For_Enum_Backed_SDL_Typedef()
    {
        // SDL_GameControllerButton is typedef'd to an enum; enum-backed SDL_*
        // typedefs should map to `int` (the enum's wire representation), not
        // IntPtr.
        var enumType = new CppEnum("SDL_GameControllerButton");
        var typedef = new CppTypedef("SDL_GameControllerButton", enumType);

        await Assert.That(TypeMappingPolicy.Map(typedef).ManagedName).IsEqualTo("int");
    }

    [Test]
    public async Task Map_Should_Fallback_To_IntPtr_For_SDL_Prefixed_Opaque_Struct_Typedef()
    {
        // SDL_Window is typedef'd to an opaque struct (CppClass) with no
        // primitive/typedef/enum resolution path. The SDL_*-prefix fallback
        // legitimately fires here — these are handle-shaped types.
        var opaqueStruct = new CppClass("SDL_Window");
        var sdlWindow = new CppTypedef("SDL_Window", opaqueStruct);

        await Assert.That(TypeMappingPolicy.Map(sdlWindow).ManagedName).IsEqualTo("IntPtr");
    }

    [Test]
    public async Task Map_Should_Represent_SDL_GUID_As_System_Guid()
    {
        var guidStruct = new CppClass("SDL_GUID")
        {
            ClassKind = CppClassKind.Struct,
            IsDefinition = true,
        };
        guidStruct.Fields.Add(new CppField(new CppArrayType(CppPrimitiveType.UnsignedChar, 16), "data"));
        var guidTypedef = new CppTypedef("SDL_GUID", guidStruct);

        await Assert.That(TypeMappingPolicy.Map(guidStruct).ManagedName).IsEqualTo("Guid");
        await Assert.That(TypeMappingPolicy.Map(guidTypedef).ManagedName).IsEqualTo("Guid");
    }

    // ─── Platform-sensitive C long width ───

    [Test]
    public async Task MapPrimitive_Should_Emit_CLong_For_C_Long()
    {
        await Assert.That(TypeMappingPolicy.MapPrimitive(CppPrimitiveType.Long).ManagedName).IsEqualTo("CLong");
    }

    [Test]
    public async Task MapPrimitive_Should_Emit_CULong_For_C_Unsigned_Long()
    {
        await Assert.That(TypeMappingPolicy.MapPrimitive(CppPrimitiveType.UnsignedLong).ManagedName).IsEqualTo("CULong");
    }

    // ─── Regression guard — fixed-width primitives ───

    [Test]
    public async Task MapPrimitive_Should_Cover_Common_C_Primitives()
    {
        await Assert.That(TypeMappingPolicy.MapPrimitive(CppPrimitiveType.Void).ManagedName).IsEqualTo("void");
        await Assert.That(TypeMappingPolicy.MapPrimitive(CppPrimitiveType.Bool).ManagedName).IsEqualTo("byte");
        await Assert.That(TypeMappingPolicy.MapPrimitive(CppPrimitiveType.Char).ManagedName).IsEqualTo("sbyte");
        await Assert.That(TypeMappingPolicy.MapPrimitive(CppPrimitiveType.UnsignedChar).ManagedName).IsEqualTo("byte");
        await Assert.That(TypeMappingPolicy.MapPrimitive(CppPrimitiveType.Short).ManagedName).IsEqualTo("short");
        await Assert.That(TypeMappingPolicy.MapPrimitive(CppPrimitiveType.UnsignedShort).ManagedName).IsEqualTo("ushort");
        await Assert.That(TypeMappingPolicy.MapPrimitive(CppPrimitiveType.Int).ManagedName).IsEqualTo("int");
        await Assert.That(TypeMappingPolicy.MapPrimitive(CppPrimitiveType.UnsignedInt).ManagedName).IsEqualTo("uint");
        await Assert.That(TypeMappingPolicy.MapPrimitive(CppPrimitiveType.LongLong).ManagedName).IsEqualTo("long");
        await Assert.That(TypeMappingPolicy.MapPrimitive(CppPrimitiveType.UnsignedLongLong).ManagedName).IsEqualTo("ulong");
        await Assert.That(TypeMappingPolicy.MapPrimitive(CppPrimitiveType.Float).ManagedName).IsEqualTo("float");
        await Assert.That(TypeMappingPolicy.MapPrimitive(CppPrimitiveType.Double).ManagedName).IsEqualTo("double");
    }

    // ─── Regression guard — SDL2 explicit-width typedef table ───

    [Test]
    public async Task Map_Should_Recognize_SDL2_Fixed_Width_Typedefs()
    {
        await Assert.That(TypeMappingPolicy.Map(new CppTypedef("Sint8", CppPrimitiveType.Char)).ManagedName).IsEqualTo("sbyte");
        await Assert.That(TypeMappingPolicy.Map(new CppTypedef("Uint8", CppPrimitiveType.UnsignedChar)).ManagedName).IsEqualTo("byte");
        await Assert.That(TypeMappingPolicy.Map(new CppTypedef("Sint16", CppPrimitiveType.Short)).ManagedName).IsEqualTo("short");
        await Assert.That(TypeMappingPolicy.Map(new CppTypedef("Uint16", CppPrimitiveType.UnsignedShort)).ManagedName).IsEqualTo("ushort");
        await Assert.That(TypeMappingPolicy.Map(new CppTypedef("Sint32", CppPrimitiveType.Int)).ManagedName).IsEqualTo("int");
        await Assert.That(TypeMappingPolicy.Map(new CppTypedef("Uint32", CppPrimitiveType.UnsignedInt)).ManagedName).IsEqualTo("uint");
        await Assert.That(TypeMappingPolicy.Map(new CppTypedef("Sint64", CppPrimitiveType.LongLong)).ManagedName).IsEqualTo("long");
        await Assert.That(TypeMappingPolicy.Map(new CppTypedef("Uint64", CppPrimitiveType.UnsignedLongLong)).ManagedName).IsEqualTo("ulong");
        await Assert.That(TypeMappingPolicy.Map(new CppTypedef("SDL_bool", CppPrimitiveType.Int)).ManagedName).IsEqualTo("int");
        await Assert.That(TypeMappingPolicy.Map(new CppTypedef("size_t", CppPrimitiveType.UnsignedLong)).ManagedName).IsEqualTo("nuint");
        await Assert.That(TypeMappingPolicy.Map(new CppTypedef("ptrdiff_t", CppPrimitiveType.Long)).ManagedName).IsEqualTo("nint");
    }

    [Test]
    public async Task Map_Should_Map_SDL2_Bool_Typedef_To_Int_Backed_ABI()
    {
        await Assert.That(TypeMappingPolicy.Map(new CppTypedef("SDL_bool", CppPrimitiveType.Int)).ManagedName).IsEqualTo("int");
        await Assert.That(TypeMappingPolicy.Map(new CppTypedef("SDL_bool", new CppEnum("SDL_bool"))).ManagedName).IsEqualTo("int");
    }

    // ─── Regression guard — pointers ───

    [Test]
    public async Task Map_Should_Emit_IntPtr_For_Void_Star()
    {
        var voidStar = new CppPointerType(CppPrimitiveType.Void);
        await Assert.That(TypeMappingPolicy.Map(voidStar).ManagedName).IsEqualTo("IntPtr");
    }

    [Test]
    public async Task Map_Should_Emit_Primitive_Star_For_Primitive_Pointer()
    {
        var intStar = new CppPointerType(CppPrimitiveType.Int);
        await Assert.That(TypeMappingPolicy.Map(intStar).ManagedName).IsEqualTo("int*");
    }

    [Test]
    public async Task Map_Should_Emit_IntPtr_For_SDL_Prefixed_Struct_Pointer()
    {
        // SDL_Window* — opaque handle pointer. This legacy mapper uses prefix-based
        // fallback; semantic translators use structural inspection when they need
        // to distinguish empty opaque classes from concrete structs.
        var sdlWindowStruct = new CppClass("SDL_Window");
        var sdlWindowStar = new CppPointerType(sdlWindowStruct);
        await Assert.That(TypeMappingPolicy.Map(sdlWindowStar).ManagedName).IsEqualTo("IntPtr");
    }

    [Test]
    public async Task Map_Should_Emit_IntPtr_For_Deferred_C_Runtime_Class_Pointer()
    {
        var filePointer = new CppPointerType(new CppClass("_IO_FILE"));

        await Assert.That(TypeMappingPolicy.Map(filePointer).ManagedName).IsEqualTo("IntPtr");
    }

    [Test]
    public async Task Map_Should_Emit_IntPtr_For_Vulkan_Dispatchable_Handle()
    {
        var vkInstance = new CppTypedef("VkInstance", new CppPointerType(new CppClass("VkInstance_T")));

        await Assert.That(TypeMappingPolicy.Map(vkInstance).ManagedName).IsEqualTo("IntPtr");
    }

    [Test]
    public async Task Map_Should_Emit_Ulong_Pointer_For_Vulkan_NonDispatchable_Handle_Pointer()
    {
        var uint64 = new CppTypedef("uint64_t", CppPrimitiveType.UnsignedLong);
        var vkSurface = new CppTypedef("VkSurfaceKHR", uint64);
        var vkSurfacePointer = new CppPointerType(vkSurface);

        await Assert.That(TypeMappingPolicy.Map(vkSurfacePointer).ManagedName).IsEqualTo("ulong*");
    }

    [Test]
    public async Task Map_Should_Emit_IntPtr_Pointer_For_GDK_Platform_Handle_Out_Parameter()
    {
        var xUserHandle = new CppTypedef("XUserHandle", new CppPointerType(CppPrimitiveType.Void));
        var outUserHandle = new CppPointerType(xUserHandle);

        await Assert.That(TypeMappingPolicy.Map(outUserHandle).ManagedName).IsEqualTo("IntPtr*");
    }

    // ─── P2.8 — typedef recursion depth guard ───

    [Test]
    public void MapTypedef_Should_Throw_When_Typedef_Chain_Exceeds_MaxDepth()
    {
        // Build a deeper-than-MaxTypedefDepth typedef chain (Aₙ → Aₙ₋₁ → ... → A0 → int)
        // to verify the depth guard fires. None of the names are in
        // ExplicitTypedefMap, so the chain walker enters MapTypedef recursion and
        // hits MaxTypedefDepth before the natural primitive leaf would resolve.
        var head = new CppTypedef("A0", CppPrimitiveType.Int);
        var tail = head;
        for (var i = 1; i <= TypeMappingPolicy.MaxTypedefDepth + 2; i++)
        {
            tail = new CppTypedef($"A{i}", tail);
        }

        Assert.Throws<InvalidOperationException>(() => TypeMappingPolicy.MapTypedef(tail));
    }

    // ─── P2.9 — SafeIdentifier covers C# 12 reserved + contextual keywords ───

    [Test]
    public async Task SafeIdentifier_Should_Escape_Reserved_Keywords()
    {
        // Reserved keywords MUST be escaped — these literally cannot appear as
        // unprefixed identifiers in C# source. Previous narrow 7-word inline
        // fallback missed contextual keywords like 'value' / 'init' / 'record'
        // that SDL2 uses as parameter names in places.
        await Assert.That(TypeMappingPolicy.SafeIdentifier("ref")).IsEqualTo("@ref");
        await Assert.That(TypeMappingPolicy.SafeIdentifier("out")).IsEqualTo("@out");
        await Assert.That(TypeMappingPolicy.SafeIdentifier("in")).IsEqualTo("@in");
        await Assert.That(TypeMappingPolicy.SafeIdentifier("string")).IsEqualTo("@string");
        await Assert.That(TypeMappingPolicy.SafeIdentifier("event")).IsEqualTo("@event");
        await Assert.That(TypeMappingPolicy.SafeIdentifier("class")).IsEqualTo("@class");
        await Assert.That(TypeMappingPolicy.SafeIdentifier("lock")).IsEqualTo("@lock");
        await Assert.That(TypeMappingPolicy.SafeIdentifier("delegate")).IsEqualTo("@delegate");
    }

    [Test]
    public async Task SafeIdentifier_Should_Escape_Contextual_Keywords()
    {
        // Contextual keywords (added across C# 1.0 → 12.0) — the previous
        // implementation only covered the 7 reserved-keyword subset, so SDL
        // parameter names like 'value' or 'var' or 'partial' would have leaked
        // through to source emit and broken compile.
        await Assert.That(TypeMappingPolicy.SafeIdentifier("value")).IsEqualTo("@value");
        await Assert.That(TypeMappingPolicy.SafeIdentifier("var")).IsEqualTo("@var");
        await Assert.That(TypeMappingPolicy.SafeIdentifier("init")).IsEqualTo("@init");
        await Assert.That(TypeMappingPolicy.SafeIdentifier("partial")).IsEqualTo("@partial");
        await Assert.That(TypeMappingPolicy.SafeIdentifier("record")).IsEqualTo("@record");
        await Assert.That(TypeMappingPolicy.SafeIdentifier("required")).IsEqualTo("@required");
        await Assert.That(TypeMappingPolicy.SafeIdentifier("scoped")).IsEqualTo("@scoped");
    }

    [Test]
    public async Task SafeIdentifier_Should_Leave_Non_Keyword_Identifiers_Unchanged()
    {
        await Assert.That(TypeMappingPolicy.SafeIdentifier("flags")).IsEqualTo("flags");
        await Assert.That(TypeMappingPolicy.SafeIdentifier("window")).IsEqualTo("window");
        await Assert.That(TypeMappingPolicy.SafeIdentifier("renderer")).IsEqualTo("renderer");
        await Assert.That(TypeMappingPolicy.SafeIdentifier("data")).IsEqualTo("data");
    }

    [Test]
    public async Task SafeIdentifier_Should_Return_Placeholder_For_Empty_Name()
    {
        // CppAst sometimes hands back empty parameter names for unnamed
        // trailing varargs — the placeholder keeps generated source compilable.
        await Assert.That(TypeMappingPolicy.SafeIdentifier(string.Empty)).IsEqualTo("@_");
    }
}
