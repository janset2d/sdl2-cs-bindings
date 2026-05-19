using Build.Data.BindingGeneration.Models;

namespace Build.Targets.GenerateBindings.Model;

/// <summary>
/// Public constant declaration in the binding surface. Drives
/// <c>CsConstantEmitter</c> at Phase 3E. The translator (Phase 3D) merges
/// AST-parsed macros with the manifest's <c>binding_generation.required_constants</c>
/// list (SDL.h-only constants that survive the umbrella exclusion the same way
/// <c>required_functions</c> recovers SDL.h-only base API).
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
