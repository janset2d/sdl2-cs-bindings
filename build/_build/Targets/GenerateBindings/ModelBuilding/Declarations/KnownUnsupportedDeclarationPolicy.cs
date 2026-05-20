using System.Collections.Immutable;
using Build.Data.BindingGeneration.Models;
using Build.Targets.GenerateBindings.ModelBuilding.Types;
using CppAst;

namespace Build.Targets.GenerateBindings.ModelBuilding.Declarations;

/// <summary>
/// Declaration-deferral filter. Consumes the
/// <see cref="BindingGenerationConfig.DeferredDeclarations"/> dictionary — entries
/// like <c>SDL_SysWMinfo</c> that the manifest carves out until typed-union
/// shape, or one-off per-family deferrals with a documented rationale — and the
/// external native type policy for C runtime internals that must not leak into
/// generated signatures.
/// <para>
/// <b>C-variadic functions are NOT filtered by this policy.</b> Per peer-evidence
/// review of SDL binding peers, the idiomatic SDL-family pattern is to emit
/// variadic functions as fmt-only raw P/Invoke (the <c>...</c> tail is dropped,
/// the <c>const char* fmt</c>
/// parameter survives) and let consumers pre-format with C# string interpolation
/// or <c>string.Format</c> before calling. A follow-up overload adds the
/// SDL2-CS-style <c>string fmtAndArglist</c> wrapper that makes the pre-format
/// expectation explicit at the API surface.
/// </para>
/// </summary>
public sealed class KnownUnsupportedDeclarationPolicy
{
    private readonly ImmutableDictionary<string, DeferredDeclarationConfig> _deferred;

    public KnownUnsupportedDeclarationPolicy(BindingGenerationConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _deferred = config.DeferredDeclarations;
    }

    /// <summary>
    /// Returns <c>true</c> when the parsed function is unsupported for the
    /// current emit per the manifest's <c>deferred_declarations</c> block.
    /// <paramref name="reason"/> carries the audit-log explanation, never
    /// <c>null</c> on the false branch.
    /// </summary>
    public bool IsUnsupported(CppFunction function, out string reason)
    {
        ArgumentNullException.ThrowIfNull(function);

        if (_deferred.TryGetValue(function.Name, out var entry))
        {
            reason = $"deferred per manifest binding_generation.deferred_declarations[{entry.Category}]: {entry.Reason}";
            return true;
        }

        if (ExternalNativeTypePolicy.TryGetDeferredReason(function, out reason))
        {
            return true;
        }

        reason = string.Empty;
        return false;
    }

    /// <summary>
    /// Name-only overload for type/struct/enum deferral checks. Bypasses the
    /// variadic branch since only functions have a <see cref="CppFunction.IsVariadic"/>
    /// flag.
    /// </summary>
    public bool IsUnsupported(string declarationName, out string reason)
    {
        ArgumentNullException.ThrowIfNull(declarationName);

        if (_deferred.TryGetValue(declarationName, out var entry))
        {
            reason = $"deferred per manifest binding_generation.deferred_declarations[{entry.Category}]: {entry.Reason}";
            return true;
        }

        reason = string.Empty;
        return false;
    }
}
