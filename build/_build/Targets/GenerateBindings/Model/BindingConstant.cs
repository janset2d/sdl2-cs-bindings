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
