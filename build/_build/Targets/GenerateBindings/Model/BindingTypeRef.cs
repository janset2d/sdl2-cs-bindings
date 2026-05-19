namespace Build.Targets.GenerateBindings.Model;

/// <summary>
/// Legacy managed-type projection used by the remaining string-based config and
/// type-mapping helpers. Semantic declaration records use <see cref="NativeTypeRef"/>;
/// this type stays only where the old projection is still a useful adapter.
/// </summary>
/// <param name="ManagedName">
/// Emitted text, e.g. <c>"int"</c>, <c>"SDL_Surface*"</c>, <c>"ReadOnlySpan&lt;byte&gt;"</c>.
/// Emitted managed text, e.g. <c>"int"</c>, <c>"SDL_Surface*"</c>,
/// <c>"ReadOnlySpan&lt;byte&gt;"</c>.
/// </param>
/// <param name="OwningFamilyId">
/// Cross-family ownership marker. <c>null</c> for primitives and managed BCL types;
/// <c>"sdl2-core"</c> for core-owned managed types referenced from satellite emits
/// (Stage 2 surface); the family id for owned types. Stage 1 only sets this to
/// <c>null</c> — the cross-family qualified-reference path activates at Stage 2.
/// </param>
/// <param name="IsPointer">
/// <c>true</c> when the managed-name represents a pointer (raw <c>T*</c> or
/// <c>IntPtr</c>). Lets emitters decide friendly-overload shape (string / Span / out)
/// without re-parsing the managed-name string.
/// </param>
/// <param name="IsOpaqueHandle">
/// <c>true</c> when the type is an opaque-handle wrapper (e.g. <c>SDL_Window</c>,
/// emitted as <c>readonly partial struct SDL_Window(nint value)</c> per Rule 2). Lets
/// <c>CsHandleEmitter</c> identify handle types without re-checking the prefix table.
/// </param>
public sealed record BindingTypeRef(
    string ManagedName,
    string? OwningFamilyId,
    bool IsPointer,
    bool IsOpaqueHandle)
{
    /// <summary>
    /// Heuristic bridge for string-based config values. Pointer detection is based
    /// on <c>EndsWith("*")</c>; semantic CppAst translation uses
    /// <see cref="NativeTypeRef"/> instead of this post-hoc projection.
    /// </summary>
    public static BindingTypeRef Of(string managedName) =>
        new(managedName, OwningFamilyId: null, IsPointer: managedName.EndsWith('*'), IsOpaqueHandle: false);
}
