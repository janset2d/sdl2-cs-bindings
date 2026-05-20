using Build.Data.BindingGeneration.Models;

namespace Build.Targets.GenerateBindings.ModelBuilding.Types;

/// <summary>
/// Immutable facts derived from <see cref="BindingGenerationConfig"/> that
/// <see cref="NativeTypeClassifier"/> needs at classification time. Keeps the
/// classifier free of the full config surface.
/// </summary>
internal sealed record NativeTypeClassificationContext(
    string FamilyId,
    IReadOnlySet<string> OwnedPrefixes,
    IReadOnlySet<string> DeferredDeclarations)
{
    public static NativeTypeClassificationContext FromConfig(BindingGenerationConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return new(
            config.FamilyId,
            config.OwnedPrefixes.ToHashSet(StringComparer.Ordinal),
            config.DeferredDeclarations.Keys.ToHashSet(StringComparer.Ordinal));
    }

    public bool IsOwned(string nativeName) =>
        OwnedPrefixes.Any(prefix => nativeName.StartsWith(prefix, StringComparison.Ordinal));

    public bool IsDeferred(string nativeName) =>
        DeferredDeclarations.Contains(nativeName);
}
