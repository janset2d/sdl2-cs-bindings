using Build.Data.Manifest;

namespace Build.Data.BindingGeneration.Models;

/// <summary>
/// Discriminator for <see cref="BindingGenerationConfigError"/>. Manifest-deserialization
/// errors (missing required fields, malformed JSON, manifest file absent) surface as
/// <see cref="Cake.Core.CakeException"/> from <see cref="ManifestRepository.Load"/>
/// — those are not part of this enum because they're caught one layer up.
/// </summary>
public enum BindingGenerationConfigErrorKind
{
    FamilyNotFound,
    Disabled,
}
