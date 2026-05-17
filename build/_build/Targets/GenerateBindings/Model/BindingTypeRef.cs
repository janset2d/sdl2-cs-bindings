namespace Build.Targets.GenerateBindings.Model;

/// <summary>
/// Managed-type reference used by every declaration record in <see cref="BindingModel"/>
/// (function return types, parameter types, struct fields, enum underlying types,
/// constant types, callback signatures). Carries enough metadata for emitters to
/// decide between local declaration vs cross-family qualified reference without
/// re-walking the CppAst node graph.
/// </summary>
/// <param name="ManagedName">
/// Emitted text, e.g. <c>"int"</c>, <c>"SDL_Surface*"</c>, <c>"ReadOnlySpan&lt;byte&gt;"</c>.
/// Phase 3D translator rewrite owns the mapping from CppAst types to this value;
/// Phase 3E per-category emitters consume it directly.
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
    /// Stage 1 bridge factory used by the CppAst translator and test fixtures to
    /// wrap the existing string-based type-flow into <see cref="BindingTypeRef"/>
    /// without re-walking the CppAst node graph. Heuristic-driven: pointer detection
    /// via <c>EndsWith("*")</c>; <c>OwningFamilyId</c> stays <c>null</c> (cross-family
    /// resolution activates at Stage 2); <c>IsOpaqueHandle</c> stays <c>false</c>
    /// (handles get the typed-struct treatment when Phase 3E's <c>CsHandleEmitter</c>
    /// lands). Phase 3D translator rewrite populates the fields directly from CppType
    /// info instead of post-hoc string inspection.
    /// </summary>
    public static BindingTypeRef Of(string managedName) =>
        new(managedName, OwningFamilyId: null, IsPointer: managedName.EndsWith('*'), IsOpaqueHandle: false);
}
