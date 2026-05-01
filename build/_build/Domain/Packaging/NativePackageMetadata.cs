using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using Build.Context;
using Build.Context.Models;
using Build.Domain.Packaging.Models;
using Build.Domain.Paths;
using Cake.Core;
using Cake.Core.IO;

namespace Build.Domain.Packaging;

/// <summary>
/// Root <c>janset-native-metadata.json</c> schema packed into every .Native nupkg.
/// Asserted current + coherent with <see cref="ManifestConfig"/> by post-pack guardrail G55.
/// </summary>
public sealed class NativePackageMetadata
{
    [JsonPropertyName("janset_family_version")]
    public required string JansetFamilyVersion { get; init; }

    [JsonPropertyName("family_identifier")]
    public required string FamilyIdentifier { get; init; }

    [JsonPropertyName("upstream_library")]
    public required string UpstreamLibrary { get; init; }

    [JsonPropertyName("upstream_version")]
    public required string UpstreamVersion { get; init; }

    [JsonPropertyName("vcpkg_port_version")]
    public int VcpkgPortVersion { get; init; }

    [JsonPropertyName("triplet_set")]
    public required IReadOnlyList<string> TripletSet { get; init; }

    [JsonPropertyName("build_commit")]
    public required string BuildCommit { get; init; }
}

/// <summary>
/// Pack-time generator for the <c>janset-native-metadata.json</c> file embedded into every
/// <c>.Native</c> nupkg. Output is the machine-readable half of the cross-referenced metadata
/// pair (the README mapping table being the human-readable half — see <see cref="ReadmeMappingTableGenerator"/>).
/// </summary>
public sealed class NativePackageMetadataGenerator(
    ManifestConfig manifestConfig,
    IPathService pathService,
    ICakeContext cakeContext) : INativePackageMetadataGenerator
{
    private readonly ManifestConfig _manifestConfig = manifestConfig ?? throw new ArgumentNullException(nameof(manifestConfig));
    private readonly IPathService _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));
    private readonly ICakeContext _cakeContext = cakeContext ?? throw new ArgumentNullException(nameof(cakeContext));

    public async Task GenerateAsync(
        PackageFamilyConfig family,
        string familyVersion,
        string buildCommitSha,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(family);
        ArgumentException.ThrowIfNullOrWhiteSpace(familyVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(buildCommitSha);

        var library = _manifestConfig.LibraryManifests.SingleOrDefault(
            candidate => string.Equals(candidate.Name, family.LibraryRef, StringComparison.OrdinalIgnoreCase));

        if (library is null)
        {
            throw new InvalidOperationException(
                $"Cannot generate native metadata for family '{family.Name}' because library_ref '{family.LibraryRef}' was not found in manifest library_manifests[].");
        }

        var triplets = _manifestConfig.Runtimes
            .Select(runtime => runtime.Triplet)
            .Where(triplet => !string.IsNullOrWhiteSpace(triplet))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(triplet => triplet, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var metadata = new NativePackageMetadata
        {
            JansetFamilyVersion = familyVersion,
            FamilyIdentifier = family.Name,
            UpstreamLibrary = library.VcpkgName,
            UpstreamVersion = library.VcpkgVersion,
            VcpkgPortVersion = library.VcpkgPortVersion,
            TripletSet = triplets,
            BuildCommit = buildCommitSha,
        };

        var targetPath = _pathService.GetHarvestLibraryNativeMetadataFile(family.LibraryRef);
        await _cakeContext.WriteJsonAsync(targetPath, metadata);

        // Enforce the same JSON file contract used across build-host modules.
        _ = await _cakeContext.ToJsonAsync<NativePackageMetadata>(targetPath);

        cancellationToken.ThrowIfCancellationRequested();
    }
}

/// <summary>
/// Post-pack validator (G55) — opens a .Native nupkg, extracts <c>janset-native-metadata.json</c>,
/// and asserts the payload matches <see cref="ManifestConfig"/> and the active build invariants.
/// </summary>
public sealed class NativePackageMetadataValidator(IFileSystem fileSystem)
{
    private readonly IFileSystem _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));

    [SuppressMessage("Design", "MA0051:Method is too long",
        Justification = "G55 intentionally validates zip presence, JSON parse, schema fields, and manifest/build invariants in one linear flow for debuggability.")]
    public async Task<PackageValidationCheck> ValidateAsync(
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
            return BuildFailure(
                family,
                nativePackagePath,
                "<native package exists>",
                "<missing>",
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
                return BuildFailure(
                    family,
                    nativePackagePath,
                    "janset-native-metadata.json",
                    "<missing>",
                    $"G55: native package '{nativePackagePath.GetFilename().FullPath}' does not contain root metadata file 'janset-native-metadata.json'.");
            }

            using var reader = new StreamReader(metadataEntry.Open());
            metadataContent = await reader.ReadToEndAsync();
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
        {
            return BuildFailure(
                family,
                nativePackagePath,
                "<readable native package>",
                ex.Message,
                $"G55: failed to read native package '{nativePackagePath.GetFilename().FullPath}' while validating metadata: {ex.Message}");
        }

        NativePackageMetadata? metadata;
        try
        {
            metadata = CakeExtensions.DeserializeJson<NativePackageMetadata>(metadataContent);
        }
        catch (JsonException ex)
        {
            return BuildFailure(
                family,
                nativePackagePath,
                "<valid metadata JSON>",
                ex.Message,
                $"G55: metadata file in '{nativePackagePath.GetFilename().FullPath}' is not valid JSON: {ex.Message}");
        }

        if (metadata is null)
        {
            return BuildFailure(
                family,
                nativePackagePath,
                "<metadata object>",
                "<null>",
                $"G55: metadata file in '{nativePackagePath.GetFilename().FullPath}' deserialized to null.");
        }

        var library = manifestConfig.LibraryManifests.SingleOrDefault(
            candidate => string.Equals(candidate.Name, family.LibraryRef, StringComparison.OrdinalIgnoreCase));

        if (library is null)
        {
            return BuildFailure(
                family,
                nativePackagePath,
                family.LibraryRef,
                "<missing library_ref>",
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
            return new PackageValidationCheck(
                FamilyIdentifier: family.Name,
                PackagePath: nativePackagePath,
                Kind: PackageValidationCheckKind.NativeMetadataFileValid,
                IsValid: true,
                ExpectedValue: "schema + manifest-consistent metadata",
                ActualValue: "valid",
                ErrorMessage: null);
        }

        return BuildFailure(
            family,
            nativePackagePath,
            "schema + manifest-consistent metadata",
            string.Join("; ", mismatches),
            $"G55: native metadata validation failed for '{nativePackagePath.GetFilename().FullPath}'. {string.Join("; ", mismatches)}");
    }

    private static PackageValidationCheck BuildFailure(
        PackageFamilyConfig family,
        FilePath nativePackagePath,
        string expected,
        string actual,
        string message)
    {
        return new PackageValidationCheck(
            FamilyIdentifier: family.Name,
            PackagePath: nativePackagePath,
            Kind: PackageValidationCheckKind.NativeMetadataFileValid,
            IsValid: false,
            ExpectedValue: expected,
            ActualValue: actual,
            ErrorMessage: message);
    }
}
