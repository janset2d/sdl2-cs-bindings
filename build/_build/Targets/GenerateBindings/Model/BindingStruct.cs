using System.Runtime.InteropServices;

namespace Build.Targets.GenerateBindings.Model;

/// <summary>
/// Public struct declaration in the binding surface. Drives
/// <c>CsStructEmitter</c> at Phase 3E. <see cref="Layout"/> + <see cref="ExplicitSize"/>
/// carry the layout policy decision (POD sequential vs explicit-offset union); typed
/// union types like SDL_syswm's <c>SDL_SysWMinfo</c> use
/// <see cref="LayoutKind.Explicit"/> with <see cref="ExplicitSize"/> set.
/// </summary>
public sealed record BindingStruct(
    string Name,
    IReadOnlyList<BindingStructField> Fields,
    LayoutKind Layout,
    int? ExplicitSize);

/// <summary>
/// One field of a <see cref="BindingStruct"/>. <see cref="FieldOffset"/> is only
/// populated when the parent struct uses <see cref="LayoutKind.Explicit"/>; for
/// sequential POD structs it stays <c>null</c>. <see cref="FixedBufferLength"/> is
/// populated for fixed-size primitive arrays such as <c>unsigned char data[16]</c>.
/// </summary>
public sealed record BindingStructField(
    string Name,
    NativeTypeRef Type,
    int? FieldOffset,
    int? FixedBufferLength = null);
