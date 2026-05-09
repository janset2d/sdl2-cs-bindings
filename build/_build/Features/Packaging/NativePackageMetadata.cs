using System.Text.Json.Serialization;
using Build.Host.Cake;
using Build.Host.Paths;
using Build.Shared.Manifest;
using Cake.Core;

namespace Build.Features.Packaging;

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
        CancellationToken ct = default)
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

        ct.ThrowIfCancellationRequested();
    }
}
