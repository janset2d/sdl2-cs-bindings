using System.Text.Json;
using System.Text.Json.Serialization;

namespace Build.Data.BindingGeneration.Models;

public sealed record GeneratedStamp
{
    [JsonPropertyName("schema_version")] public int SchemaVersion { get; init; } = 2;
    [JsonPropertyName("family")] public required string Family { get; init; }
    [JsonPropertyName("library_name")] public required string LibraryName { get; init; }
    [JsonPropertyName("library_version")] public required string LibraryVersion { get; init; }
    [JsonPropertyName("library_port_version")] public int LibraryPortVersion { get; init; }
    [JsonPropertyName("generator_source_fingerprint")] public required string GeneratorSourceFingerprint { get; init; }
    [JsonPropertyName("generator_inputs")] public required IReadOnlyDictionary<string, string> GeneratorInputs { get; init; }
    [JsonPropertyName("toolchain")] public required JsonElement Toolchain { get; init; }
    [JsonPropertyName("config_fingerprint")] public required string ConfigFingerprint { get; init; }
    [JsonPropertyName("config_inputs")] public required JsonElement ConfigInputs { get; init; }
    [JsonPropertyName("rsp_inputs")] public required IReadOnlyDictionary<string, string> RspInputs { get; init; }
    [JsonPropertyName("shim_inputs")] public required IReadOnlyDictionary<string, string> ShimInputs { get; init; }
    [JsonPropertyName("vcpkg_manifest_hash")] public required string VcpkgManifestHash { get; init; }
    [JsonPropertyName("vcpkg_baseline")] public required string VcpkgBaseline { get; init; }
    [JsonPropertyName("vcpkg_inputs")] public required IReadOnlyDictionary<string, string> VcpkgInputs { get; init; }
    [JsonPropertyName("vcpkg_triplet")] public required string VcpkgTriplet { get; init; }
    [JsonPropertyName("use_platform_header_shims")] public bool UsePlatformHeaderShims { get; init; }
    [JsonPropertyName("codegen_passes")] public required IReadOnlyList<string> CodegenPasses { get; init; }
    [JsonPropertyName("parse_views")] public required JsonElement ParseViews { get; init; }
    [JsonPropertyName("selected_headers")] public required IReadOnlyList<string> SelectedHeaders { get; init; }
    [JsonPropertyName("header_count")] public int HeaderCount { get; init; }
    [JsonPropertyName("header_inputs")] public required IReadOnlyDictionary<string, string> HeaderInputs { get; init; }
    [JsonPropertyName("header_fingerprint")] public required string HeaderFingerprint { get; init; }
    [JsonPropertyName("header_input_count")] public int HeaderInputCount { get; init; }
    [JsonPropertyName("generated_files")] public required IReadOnlyDictionary<string, string> GeneratedFiles { get; init; }
    [JsonPropertyName("generated_file_count")] public int GeneratedFileCount { get; init; }
}
