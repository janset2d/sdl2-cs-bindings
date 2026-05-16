using System.Text.Json.Serialization;

namespace Build.Data.BindingGeneration.Models;

public sealed record GeneratedStamp(
    [property: JsonPropertyName("schema_version")]
    int SchemaVersion,
    [property: JsonPropertyName("family")]
    string Family,
    [property: JsonPropertyName("generator_assembly")]
    string GeneratorAssembly,
    [property: JsonPropertyName("cppast_version")]
    string CppAstVersion,
    [property: JsonPropertyName("libclang_version")]
    string LibClangVersion,
    [property: JsonPropertyName("vcpkg_triplet")]
    string VcpkgTriplet,
    [property: JsonPropertyName("vcpkg_manifest_hash")]
    string VcpkgManifestHash,
    [property: JsonPropertyName("vcpkg_baseline")]
    string VcpkgBaseline,
    [property: JsonPropertyName("manifest_library_version")]
    string ManifestLibraryVersion,
    [property: JsonPropertyName("header_fingerprint")]
    string HeaderFingerprint,
    [property: JsonPropertyName("header_count")]
    int HeaderCount,
    [property: JsonPropertyName("parse_views")]
    IReadOnlyList<string> ParseViews);
