using System.Text.Json.Serialization;

namespace Build.Data.NativePackageMetadata;

/// <summary>
/// Root <c>janset-native-metadata.json</c> schema packed into every .Native nupkg.
/// Asserted current + coherent with <c>ManifestConfig</c> by post-pack guardrail G55.
/// </summary>
public sealed class NativePackageMetadataDocument
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
