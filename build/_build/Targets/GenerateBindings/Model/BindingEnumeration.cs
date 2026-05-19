namespace Build.Targets.GenerateBindings.Model;

/// <summary>
/// Public enum declaration in the binding surface. Drives <c>CsEnumEmitter</c>.
/// <see cref="IsFlags"/> controls whether <c>[Flags]</c> is emitted;
/// the translator decides via SDL2 naming convention (<c>SDL_*_FLAG</c> /
/// <c>*_MASK</c>) plus structural inspection (powers-of-two members). Named
/// <c>BindingEnumeration</c> rather than <c>BindingEnum</c> per CA1711 — the
/// type models a C enum declaration; the <c>Enum</c> suffix is reserved for
/// types that derive from <see cref="System.Enum"/>.
/// </summary>
public sealed record BindingEnumeration(
    string Name,
    NativeTypeRef UnderlyingType,
    bool IsFlags,
    IReadOnlyList<BindingEnumMember> Members);

/// <summary>
/// One member of a <see cref="BindingEnumeration"/>. <see cref="Value"/> is the
/// literal form as emitted in the .g.cs source (e.g. <c>"0"</c>, <c>"1 &lt;&lt; 4"</c>,
/// <c>"SDL_BUTTON(1)"</c> after macro expansion).
/// </summary>
public sealed record BindingEnumMember(string Name, string Value);
