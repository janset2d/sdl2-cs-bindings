using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Build.Context.Models;

public record ManifestConfig
{
    [JsonPropertyName("library_manifests")]
    public required IImmutableList<LibraryManifest> LibraryManifests { get; init; }
}

public record LibraryManifest
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("vcpkg_name")]
    public required string VcpkgName { get; init; }

    [JsonPropertyName("vcpkg_version")]
    public required string VcpkgVersion { get; init; }

    [JsonPropertyName("vcpkg_port_version")]
    public required int VcpkgPortVersion { get; init; }

    [JsonPropertyName("native_lib_name")]
    public required string NativeLibName { get; init; }

    [JsonPropertyName("native_lib_version")]
    public required string NativeLibVersion { get; init; }

    [JsonPropertyName("core_lib")]
    public required bool IsCoreLib { get; init; }

    [JsonPropertyName("lib_names")]
    public required IImmutableList<LibName> LibNames { get; init; }
}

public record LibName
{
    [JsonPropertyName("os")] public required string Os { get; init; }

    [JsonPropertyName("name")] public required string Name { get; init; } = string.Empty;
}
