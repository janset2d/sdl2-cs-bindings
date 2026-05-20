using Build.Data.BindingGeneration.Models;

namespace Build.Targets.GenerateBindings.Model;

/// <summary>
/// Public constant declaration in the binding surface. Produced by
/// <c>BindingConstantTranslator</c> from source-parsed macros (via
/// <c>MacroCandidateCollector</c> → <c>MacroValueClassifier</c> → <c>MacroConstantMerger</c>),
/// with required constants from the manifest's <c>binding_generation.required_constants</c>
/// injected by <c>MacroManualPolicyApplier</c> for headers excluded from the per-header
/// parse loop (e.g. SDL.h).
/// </summary>
/// <param name="Name">Identifier as it appears in C (e.g. <c>SDL_INIT_TIMER</c>).</param>
/// <param name="Type">Managed type ref (e.g. <c>uint</c>).</param>
/// <param name="Value">
/// Literal value text emitted verbatim (e.g. <c>"0x00000001u"</c> for
/// <see cref="ConstantKind.Literal"/>; the compound expression for
/// <see cref="ConstantKind.Computed"/>).
/// </param>
/// <param name="Kind">C macro-shape metadata. See <see cref="ConstantKind"/>.</param>
public sealed record BindingConstant(string Name, NativeTypeRef Type, string Value, ConstantKind Kind);

/// <summary>
/// Opaque-handle type (e.g. <c>SDL_Window</c>, <c>SDL_Renderer</c>, <c>SDL_Texture</c>).
/// <c>HandleEmitter</c> emits each as
/// <c>readonly partial struct &lt;Name&gt;(nint value)</c> with the full Rule 2
/// feature set: <c>IsNull</c>, <c>Null</c>, <c>IEquatable&lt;T&gt;</c>, implicit
/// <c>nint</c> conversion, equality operators, <c>[DebuggerDisplay]</c>. <see cref="Type"/>
/// carries the semantic native type for cross-family and emitter queries.
/// </summary>
public sealed record BindingHandle(string Name, NativeTypeRef Type);

/// <summary>
/// Public function-pointer typedef in the binding surface (e.g.
/// <c>SDL_EventFilter</c>, <c>SDL_AudioCallback</c>, <c>SDL_HintCallback</c>).
/// <c>CallbackEmitter</c> emits these as
/// <c>delegate* unmanaged[Cdecl]&lt;...&gt;</c> where representable;
/// <c>[UnmanagedFunctionPointer]</c> + <c>delegate</c> shapes lend themselves to
/// legacy-TFM friendly overloads.
/// </summary>
public sealed record BindingCallback(
    string Name,
    NativeTypeRef ReturnType,
    IReadOnlyList<BindingParameter> Parameters);
