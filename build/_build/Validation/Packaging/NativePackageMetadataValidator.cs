using System.Diagnostics.CodeAnalysis;
using Build.Data.NativePackageMetadata;
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
    Task<ValidationCheck?> ValidateAsync(
        PackageFamilyConfig family,
        FilePath nativePackagePath,
        string expectedFamilyVersion,
        string expectedCommitSha,
        ManifestConfig manifestConfig);
}

/// <inheritdoc />
public sealed class NativePackageMetadataValidator(INativePackageMetadataRepository nativePackageMetadataRepository) : INativePackageMetadataValidator
{
    private readonly INativePackageMetadataRepository _nativePackageMetadataRepository = nativePackageMetadataRepository ?? throw new ArgumentNullException(nameof(nativePackageMetadataRepository));

    [SuppressMessage("Design", "MA0051:Method is too long",
        Justification = "G55 intentionally validates zip presence, JSON parse, schema fields, and manifest/build invariants in one linear flow for debuggability.")]
    public async Task<ValidationCheck?> ValidateAsync(
        PackageFamilyConfig family,
        FilePath nativePackagePath,
        string expectedFamilyVersion,
        string expectedCommitSha,
        ManifestConfig manifestConfig)
    {
        ArgumentNullException.ThrowIfNull(family);
        ArgumentNullException.ThrowIfNull(nativePackagePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedFamilyVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedCommitSha);
        ArgumentNullException.ThrowIfNull(manifestConfig);

        var metadataResult = await _nativePackageMetadataRepository
            .ReadFromPackageAsync(nativePackagePath)
            .ConfigureAwait(false);
        if (metadataResult.IsFailure)
        {
            return Failure($"G55: {metadataResult.Error.Message}");
        }

        var metadata = metadataResult.Value;

        var library = manifestConfig.LibraryManifests.SingleOrDefault(
            candidate => string.Equals(candidate.Name, family.LibraryRef, StringComparison.OrdinalIgnoreCase));

        if (library is null)
        {
            return Failure(
                $"G55: cannot validate metadata for family '{family.Name}' because library_ref '{family.LibraryRef}' is missing in manifest library_manifests[].");
        }

        var expectedTripletSet = manifestConfig.Runtimes
            .Select(runtime => runtime.Triplet)
            .Where(triplet => !string.IsNullOrWhiteSpace(triplet))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(triplet => triplet, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var actualTripletSet = (metadata.TripletSet ?? [])
            .Where(triplet => !string.IsNullOrWhiteSpace(triplet))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(triplet => triplet, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var mismatches = new List<string>();

        if (!string.Equals(metadata.JansetFamilyVersion, expectedFamilyVersion, StringComparison.Ordinal))
        {
            mismatches.Add($"janset_family_version expected '{expectedFamilyVersion}' actual '{metadata.JansetFamilyVersion}'");
        }

        if (!string.Equals(metadata.FamilyIdentifier, family.Name, StringComparison.OrdinalIgnoreCase))
        {
            mismatches.Add($"family_identifier expected '{family.Name}' actual '{metadata.FamilyIdentifier}'");
        }

        if (!string.Equals(metadata.UpstreamLibrary, library.VcpkgName, StringComparison.OrdinalIgnoreCase))
        {
            mismatches.Add($"upstream_library expected '{library.VcpkgName}' actual '{metadata.UpstreamLibrary}'");
        }

        if (!string.Equals(metadata.UpstreamVersion, library.VcpkgVersion, StringComparison.Ordinal))
        {
            mismatches.Add($"upstream_version expected '{library.VcpkgVersion}' actual '{metadata.UpstreamVersion}'");
        }

        if (metadata.VcpkgPortVersion != library.VcpkgPortVersion)
        {
            mismatches.Add($"vcpkg_port_version expected '{library.VcpkgPortVersion}' actual '{metadata.VcpkgPortVersion}'");
        }

        if (!string.Equals(metadata.BuildCommit, expectedCommitSha, StringComparison.OrdinalIgnoreCase))
        {
            mismatches.Add($"build_commit expected '{expectedCommitSha}' actual '{metadata.BuildCommit}'");
        }

        if (!actualTripletSet.SequenceEqual(expectedTripletSet, StringComparer.OrdinalIgnoreCase))
        {
            mismatches.Add($"triplet_set expected '[{string.Join(", ", expectedTripletSet)}]' actual '[{string.Join(", ", actualTripletSet)}]'");
        }

        if (mismatches.Count == 0)
        {
            return null;
        }

        return Failure(
            $"G55: native metadata validation failed for '{nativePackagePath.GetFilename().FullPath}'. {string.Join("; ", mismatches)}");
    }

    private static ValidationCheck Failure(string message) => new("Native metadata", ValidationSeverity.Error, message, Code: "G55");
}
