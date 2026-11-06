using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Build.Data.BindingGeneration.Models;

namespace Build.Data.Manifest.Models;

public sealed record LibraryManifest
{
    [JsonPropertyName("name")] public required string Name { get; init; }
    [JsonPropertyName("vcpkg_name")] public required string VcpkgName { get; init; }
    [JsonPropertyName("vcpkg_version")] public required string VcpkgVersion { get; init; }
    [JsonPropertyName("vcpkg_port_version")] public required int VcpkgPortVersion { get; init; }
    [JsonPropertyName("native_lib_name")] public required string NativeLibName { get; init; }
    [JsonPropertyName("core_lib")] public required bool IsCoreLib { get; init; }
    [JsonPropertyName("primary_binaries")] public required IImmutableList<PrimaryBinary> PrimaryBinaries { get; init; }
    [JsonPropertyName("binding_generation")] public required BindingGenerationConfig BindingGeneration { get; init; }
}
