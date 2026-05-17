namespace Build.Data.BindingGeneration.Models;

/// <summary>
/// Discriminator for <see cref="BindingGenerationConfigError"/>. Manifest-deserialization
/// errors (missing required fields, malformed JSON, manifest file absent) surface as
/// <see cref="Cake.Core.CakeException"/> from <see cref="Build.Data.Manifest.ManifestRepository.Load"/>
/// — those are not part of this enum because they're caught one layer up.
/// </summary>
public enum BindingGenerationConfigErrorKind
{
    FamilyNotFound,
    Disabled,
}

/// <summary>
/// Expected failure surface for <see cref="BindingGenerationConfigRepository.Load"/>.
/// Pre-deserialization shape errors are <see cref="Cake.Core.CakeException"/>
/// from the underlying manifest repository; this type only carries the post-deserialization
/// per-family failures (family not in manifest, family explicitly disabled).
/// Flat record + factory pattern matches <see cref="ManifestResolutionError"/>.
/// </summary>
public sealed record BindingGenerationConfigError(string Reason, BindingGenerationConfigErrorKind Kind)
{
    public static BindingGenerationConfigError FamilyNotFound(string familyId) =>
        new($"Manifest does not declare a library with binding_generation matching family '{familyId}'.",
            BindingGenerationConfigErrorKind.FamilyNotFound);

    public static BindingGenerationConfigError Disabled(string familyId) =>
        new($"Family '{familyId}' has binding_generation.enabled=false in manifest.",
            BindingGenerationConfigErrorKind.Disabled);
}
