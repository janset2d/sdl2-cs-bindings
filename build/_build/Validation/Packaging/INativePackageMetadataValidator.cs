using Build.Data.Manifest.Models;
using Build.Results;
using Cake.Core.IO;

namespace Build.Validation.Packaging;

/// <summary>
/// Post-pack validator (G55) that asserts the native package metadata payload matches
/// <see cref="ManifestConfig"/> and the active build invariants. Returns
/// <see langword="null"/> when the metadata is consistent with the manifest, or a single
/// <see cref="ValidationCheck"/> aggregating every drift the validator found.
/// </summary>
public interface INativePackageMetadataValidator
{
    public Task<ValidationCheck?> ValidateAsync(
        PackageFamilyConfig family,
        FilePath nativePackagePath,
        string expectedFamilyVersion,
        string expectedCommitSha,
        ManifestConfig manifestConfig);
}
