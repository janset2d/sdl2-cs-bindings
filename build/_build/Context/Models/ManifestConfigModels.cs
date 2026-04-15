using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Build.Modules.Strategy.Models;

namespace Build.Context.Models;

public record ManifestConfig
{
    [JsonPropertyName("schema_version")]
    public required string SchemaVersion { get; init; }

    [JsonPropertyName("packaging_config")]
    public required PackagingConfig PackagingConfig { get; init; }

    [JsonPropertyName("runtimes")]
    public required IImmutableList<RuntimeInfo> Runtimes { get; init; }

    [JsonPropertyName("package_families")]
    public required IImmutableList<PackageFamilyConfig> PackageFamilies { get; init; }

    [JsonPropertyName("system_exclusions")]
    public required SystemArtefactsConfig SystemExclusions { get; init; }

    [JsonPropertyName("library_manifests")]
    public required IImmutableList<LibraryManifest> LibraryManifests { get; init; }
}

public record PackagingConfig
{
    [JsonPropertyName("validation_mode")]
    [JsonConverter(typeof(JsonStringEnumConverter<ValidationMode>))]
    public required ValidationMode ValidationMode { get; init; }

    [JsonPropertyName("core_library")]
    public required string CoreLibrary { get; init; }
}

public record PackageFamilyConfig
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("tag_prefix")]
    public required string TagPrefix { get; init; }

    [JsonPropertyName("managed_project")]
    public string? ManagedProject { get; init; }

    [JsonPropertyName("native_project")]
    public string? NativeProject { get; init; }

    [JsonPropertyName("library_ref")]
    public required string LibraryRef { get; init; }

    [JsonPropertyName("depends_on")]
    public required IImmutableList<string> DependsOn { get; init; }

    [JsonPropertyName("change_paths")]
    public required IImmutableList<string> ChangePaths { get; init; }
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
