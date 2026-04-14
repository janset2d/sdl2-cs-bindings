using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Build.Context.Models;

public record ManifestConfig
{
    [JsonPropertyName("schema_version")]
    public string? SchemaVersion { get; init; }

    [JsonPropertyName("packaging_config")]
    public PackagingConfig? PackagingConfig { get; init; }

    [JsonPropertyName("runtimes")]
    public IImmutableList<RuntimeInfo>? Runtimes { get; init; }

    [JsonPropertyName("system_exclusions")]
    public SystemArtefactsConfig? SystemExclusions { get; init; }

    [JsonPropertyName("library_manifests")]
    public required IImmutableList<LibraryManifest> LibraryManifests { get; init; }
}

public record PackagingConfig
{
    [JsonPropertyName("validation_mode")]
    public string? ValidationMode { get; init; }

    [JsonPropertyName("core_library")]
    public string? CoreLibrary { get; init; }
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

    [JsonPropertyName("primary_binaries")]
    public required IImmutableList<PrimaryBinary> PrimaryBinaries { get; init; }
}

public record PrimaryBinary
{
    [JsonPropertyName("os")]
    public required string Os { get; init; }

    [JsonPropertyName("patterns")]
    public required IImmutableList<string> Patterns { get; init; }
}
