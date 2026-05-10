using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Text.Json;
using Build.Host.Cake;
using Build.Manifest;
using Build.Targets.Package.Models;
using Build.Results;
using Cake.Core.IO;

namespace Build.Validation.Packaging;

/// <summary>
/// Post-pack validator (G55) — opens a .Native nupkg, extracts <c>janset-native-metadata.json</c>,
/// and asserts the payload matches <see cref="ManifestConfig"/> and the active build invariants.
/// Returns <see langword="null"/> when the metadata is consistent with the manifest, or a single
/// <see cref="ValidationCheck"/> aggregating every drift the validator found.
/// </summary>
public sealed class NativePackageMetadataValidator(IFileSystem fileSystem) : INativePackageMetadataValidator
{
    private readonly IFileSystem _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));

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

        var file = _fileSystem.GetFile(nativePackagePath);
        if (!file.Exists)
        {
            return Failure(
                $"G55: native package '{nativePackagePath.GetFilename().FullPath}' is missing, metadata file cannot be validated.");
        }

        string? metadataContent;
        try
        {
            await using var stream = file.OpenRead();
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
            var metadataEntry = archive.Entries.SingleOrDefault(entry =>
                string.Equals(entry.FullName, "janset-native-metadata.json", StringComparison.OrdinalIgnoreCase));

            if (metadataEntry is null)
            {
                return Failure(
                    $"G55: native package '{nativePackagePath.GetFilename().FullPath}' does not contain root metadata file 'janset-native-metadata.json'.");
            }

#pragma warning disable CA1849, S6966 // ZipArchiveEntry.Open sync used intentionally for small metadata reads
            using var reader = new StreamReader(metadataEntry.Open());
#pragma warning restore CA1849, S6966
            metadataContent = await reader.ReadToEndAsync();
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
        {
            return Failure(
                $"G55: failed to read native package '{nativePackagePath.GetFilename().FullPath}' while validating metadata: {ex.Message}");
        }

        NativePackageMetadata? metadata;
        try
        {
            metadata = CakeJsonExtensions.DeserializeJson<NativePackageMetadata>(metadataContent);
        }
        catch (JsonException ex)
        {
            return Failure(
                $"G55: metadata file in '{nativePackagePath.GetFilename().FullPath}' is not valid JSON: {ex.Message}");
        }

        if (metadata is null)
        {
            return Failure(
                $"G55: metadata file in '{nativePackagePath.GetFilename().FullPath}' deserialized to null.");
        }

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

    private static ValidationCheck Failure(string message)
        => new("Native metadata", ValidationSeverity.Error, message, Code: "G55");
}
