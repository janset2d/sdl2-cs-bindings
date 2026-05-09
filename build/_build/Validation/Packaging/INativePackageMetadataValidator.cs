using Build.Results;
using Build.Shared.Manifest;
using Cake.Core.IO;

namespace Build.Validation.Packaging;

/// <summary>
/// Post-pack validator for the <c>janset-native-metadata.json</c> file embedded into
/// every <c>.Native</c> nupkg (release-guardrails [G55]). Returns <see langword="null"/>
/// when the metadata is consistent with the manifest, or a single <see cref="ValidationCheck"/>
/// aggregating every drift the validator found.
/// </summary>
public interface INativePackageMetadataValidator
{
    Task<ValidationCheck?> ValidateAsync(
        PackageFamilyConfig family,
        FilePath nativePackagePath,
        string expectedFamilyVersion,
        string expectedCommitSha,
        ManifestConfig manifestConfig);
}
