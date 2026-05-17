using Build.Targets.GenerateBindings.Model;
using CppAst;

namespace Build.Tests.Unit.Targets.GenerateBindings.Model;

// Targets CppAstToBindingModel.MapType / MapPrimitive / MapTypedef / MapPointer.
// Stage 1 Task 3.5 Post-Implementation Review surfaced wire-format bugs where
// the previous private-static type-mapping silently emitted IntPtr (8 bytes)
// for SDL_*-prefixed primitive typedefs and emitted 32-bit `int` for C `long`
// on LP64 targets (Linux/macOS). The translator's symbol-name validator gate
// stayed green throughout. Mapping logic is now exercised directly so the same
// class of bug cannot land undetected again.
//
// CppPrimitiveType has no public constructor — its instances are static
// singletons (CppPrimitiveType.Int, CppPrimitiveType.UnsignedShort, etc.).
// Tests reference those.
public sealed class CppAstToBindingModelMappingTests
{
    // ─── P0.1 — MapTypedef must chain-resolve before SDL_*-prefix fallback ───

    [Test]
    public async Task MapType_Should_Resolve_SDL_AudioFormat_Typedef_To_Underlying_Ushort()
    {
        // typedef Uint16 SDL_AudioFormat — Uint16 is a known explicit-width SDL2
        // typedef that maps to `ushort`. SDL_AudioFormat then resolves through
        // its element (Uint16) and inherits `ushort`. Previously the SDL_*
        // prefix fallback fired first and emitted IntPtr (8 bytes).
        var uint16Typedef = new CppTypedef("Uint16", CppPrimitiveType.UnsignedShort);
        var sdlAudioFormat = new CppTypedef("SDL_AudioFormat", uint16Typedef);

        await Assert.That(CppAstToBindingModel.MapType(sdlAudioFormat)).IsEqualTo("ushort");
    }

    [Test]
    public async Task MapType_Should_Resolve_SDL_SpinLock_Typedef_To_Underlying_Int()
    {
        var sdlSpinLock = new CppTypedef("SDL_SpinLock", CppPrimitiveType.Int);

        await Assert.That(CppAstToBindingModel.MapType(sdlSpinLock)).IsEqualTo("int");
    }

    [Test]
    public async Task MapType_Should_Emit_Int_For_Enum_Backed_SDL_Typedef()
    {
        // SDL_GameControllerButton is typedef'd to an enum; enum-backed SDL_*
        // typedefs should map to `int` (the enum's wire representation), not
        // IntPtr.
        var enumType = new CppEnum("SDL_GameControllerButton");
        var typedef = new CppTypedef("SDL_GameControllerButton", enumType);

        await Assert.That(CppAstToBindingModel.MapType(typedef)).IsEqualTo("int");
    }

    [Test]
    public async Task MapType_Should_Fallback_To_IntPtr_For_SDL_Prefixed_Opaque_Struct_Typedef()
    {
        // SDL_Window is typedef'd to an opaque struct (CppClass) with no
        // primitive/typedef/enum resolution path. The SDL_*-prefix fallback
        // legitimately fires here — these are handle-shaped types.
        var opaqueStruct = new CppClass("SDL_Window");
        var sdlWindow = new CppTypedef("SDL_Window", opaqueStruct);

        await Assert.That(CppAstToBindingModel.MapType(sdlWindow)).IsEqualTo("IntPtr");
    }

    // ─── P0.2 — Linux `long` width on LP64 ───

    [Test]
    public async Task MapPrimitive_Should_Emit_Nint_For_C_Long_To_Round_Trip_LP64_And_LLP64()
    {
        // C `long` is 64-bit on LP64 (Linux x86_64, Linux arm64, macOS x86_64,
        // macOS arm64) and 32-bit on LLP64 (Windows). Stage 1 parses Linux
        // headers; emitting `int` would truncate to 32-bit on the LP64 runtime
        // ABI and silently corrupt the wire format. `nint` is platform-sized at
        // CLR runtime, round-trips correctly on both target families.
        await Assert.That(CppAstToBindingModel.MapPrimitive(CppPrimitiveType.Long)).IsEqualTo("nint");
    }

    [Test]
    public async Task MapPrimitive_Should_Emit_Nuint_For_C_Unsigned_Long()
    {
        await Assert.That(CppAstToBindingModel.MapPrimitive(CppPrimitiveType.UnsignedLong)).IsEqualTo("nuint");
    }

    // ─── Regression guard — fixed-width primitives ───

    [Test]
    public async Task MapPrimitive_Should_Cover_Common_C_Primitives()
    {
        await Assert.That(CppAstToBindingModel.MapPrimitive(CppPrimitiveType.Void)).IsEqualTo("void");
        await Assert.That(CppAstToBindingModel.MapPrimitive(CppPrimitiveType.Bool)).IsEqualTo("byte");
        await Assert.That(CppAstToBindingModel.MapPrimitive(CppPrimitiveType.Char)).IsEqualTo("sbyte");
        await Assert.That(CppAstToBindingModel.MapPrimitive(CppPrimitiveType.UnsignedChar)).IsEqualTo("byte");
        await Assert.That(CppAstToBindingModel.MapPrimitive(CppPrimitiveType.Short)).IsEqualTo("short");
        await Assert.That(CppAstToBindingModel.MapPrimitive(CppPrimitiveType.UnsignedShort)).IsEqualTo("ushort");
        await Assert.That(CppAstToBindingModel.MapPrimitive(CppPrimitiveType.Int)).IsEqualTo("int");
        await Assert.That(CppAstToBindingModel.MapPrimitive(CppPrimitiveType.UnsignedInt)).IsEqualTo("uint");
        await Assert.That(CppAstToBindingModel.MapPrimitive(CppPrimitiveType.LongLong)).IsEqualTo("long");
        await Assert.That(CppAstToBindingModel.MapPrimitive(CppPrimitiveType.UnsignedLongLong)).IsEqualTo("ulong");
        await Assert.That(CppAstToBindingModel.MapPrimitive(CppPrimitiveType.Float)).IsEqualTo("float");
        await Assert.That(CppAstToBindingModel.MapPrimitive(CppPrimitiveType.Double)).IsEqualTo("double");
    }

    // ─── Regression guard — SDL2 explicit-width typedef table ───

    [Test]
    public async Task MapType_Should_Recognize_SDL2_Fixed_Width_Typedefs()
    {
        await Assert.That(CppAstToBindingModel.MapType(new CppTypedef("Sint8", CppPrimitiveType.Char))).IsEqualTo("sbyte");
        await Assert.That(CppAstToBindingModel.MapType(new CppTypedef("Uint8", CppPrimitiveType.UnsignedChar))).IsEqualTo("byte");
        await Assert.That(CppAstToBindingModel.MapType(new CppTypedef("Sint16", CppPrimitiveType.Short))).IsEqualTo("short");
        await Assert.That(CppAstToBindingModel.MapType(new CppTypedef("Uint16", CppPrimitiveType.UnsignedShort))).IsEqualTo("ushort");
        await Assert.That(CppAstToBindingModel.MapType(new CppTypedef("Sint32", CppPrimitiveType.Int))).IsEqualTo("int");
        await Assert.That(CppAstToBindingModel.MapType(new CppTypedef("Uint32", CppPrimitiveType.UnsignedInt))).IsEqualTo("uint");
        await Assert.That(CppAstToBindingModel.MapType(new CppTypedef("Sint64", CppPrimitiveType.LongLong))).IsEqualTo("long");
        await Assert.That(CppAstToBindingModel.MapType(new CppTypedef("Uint64", CppPrimitiveType.UnsignedLongLong))).IsEqualTo("ulong");
        await Assert.That(CppAstToBindingModel.MapType(new CppTypedef("SDL_bool", CppPrimitiveType.Int))).IsEqualTo("byte");
        await Assert.That(CppAstToBindingModel.MapType(new CppTypedef("size_t", CppPrimitiveType.UnsignedLong))).IsEqualTo("nuint");
        await Assert.That(CppAstToBindingModel.MapType(new CppTypedef("ptrdiff_t", CppPrimitiveType.Long))).IsEqualTo("nint");
    }

    // ─── Regression guard — pointers ───

    [Test]
    public async Task MapType_Should_Emit_IntPtr_For_Void_Star()
    {
        var voidStar = new CppPointerType(CppPrimitiveType.Void);
        await Assert.That(CppAstToBindingModel.MapType(voidStar)).IsEqualTo("IntPtr");
    }

    [Test]
    public async Task MapType_Should_Emit_Primitive_Star_For_Primitive_Pointer()
    {
        var intStar = new CppPointerType(CppPrimitiveType.Int);
        await Assert.That(CppAstToBindingModel.MapType(intStar)).IsEqualTo("int*");
    }

    [Test]
    public async Task MapType_Should_Emit_IntPtr_For_SDL_Prefixed_Struct_Pointer()
    {
        // SDL_Window* — opaque handle pointer.
        var sdlWindowStruct = new CppClass("SDL_Window");
        var sdlWindowStar = new CppPointerType(sdlWindowStruct);
        await Assert.That(CppAstToBindingModel.MapType(sdlWindowStar)).IsEqualTo("IntPtr");
    }
}
