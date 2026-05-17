using Build.Data.BindingGeneration.Models;

namespace Build.Targets.GenerateBindings.Translation;

/// <summary>
/// Family-scoped identity policy — answers "is identifier Y owned by this family?"
/// and "what qualified managed reference does a satellite emitter use to point at
/// a core-owned type?". See unified design spec §8.1 (identity vs category split):
/// prefix-based identity stays here because manifest already declares it
/// (<see cref="BindingGenerationConfig.OwnedPrefixes"/>) and names are well-disciplined
/// within each SDL family; type category (handle vs value vs struct vs enum) lives
/// in Phase 3D translator structural inspection, NOT in another prefix list here.
/// </summary>
public sealed class CoreOwnedTypeMap
{
    private readonly IReadOnlyList<string> _ownedPrefixes;
    private readonly string _coreManagedNamespace;
    private readonly string _coreFamilyId;

    public CoreOwnedTypeMap(BindingGenerationConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _ownedPrefixes = config.OwnedPrefixes;
        _coreManagedNamespace = $"Janset.{config.ManagedNamespace}";
        _coreFamilyId = config.FamilyId;
    }

    /// <summary>Family id of the owning family — e.g. <c>"sdl2-core"</c>.</summary>
    public string CoreFamilyId => _coreFamilyId;

    /// <summary>
    /// Returns <c>true</c> when <paramref name="identifier"/> starts with any of
    /// the family's declared owned prefixes. Stage 1 sdl2-core declares
    /// <c>["SDL_", "SDLK_", "SDL_HINT_", "SDL_INIT_"]</c>; Stage 2 satellites
    /// declare their own (e.g. <c>["IMG_"]</c> for sdl2-image).
    /// </summary>
    public bool IsOwned(string identifier)
    {
        ArgumentNullException.ThrowIfNull(identifier);
        return identifier.Length != 0
            && _ownedPrefixes.Any(p => identifier.StartsWith(p, StringComparison.Ordinal));
    }

    /// <summary>
    /// Builds the cross-family qualified reference for satellite emit contexts.
    /// e.g. <c>"SDL_Surface"</c> → <c>"Janset.SDL2.SDL_Surface"</c> in
    /// <c>sdl2-image</c> generated source. Stage 1 doesn't exercise this path
    /// (only sdl2-core is enabled); activates at Stage 2 satellite generation.
    /// </summary>
    public string QualifiedManagedReference(string identifier)
    {
        ArgumentNullException.ThrowIfNull(identifier);
        return $"{_coreManagedNamespace}.{identifier}";
    }
}
