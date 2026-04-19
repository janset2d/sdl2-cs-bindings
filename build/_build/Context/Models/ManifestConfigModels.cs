using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Build.Domain.Strategy.Models;

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

    /// <summary>
    /// Materialized view of the single core library declared in <see cref="LibraryManifests"/>.
    /// Consumers that need to know "which vcpkg package is the core library" should read
    /// <c>CoreLibrary.VcpkgName</c> via this property rather than re-scanning the list or
    /// reading <see cref="PackagingConfig.CoreLibrary"/> directly — those two fields must
    /// agree, and the agreement is enforced by the PreFlight <c>CoreLibraryIdentityValidator</c>
    /// (guardrail G49). This property throws only when the manifest is structurally broken
    /// (zero or multiple <c>core_lib: true</c> entries); the cross-field drift surface is a
    /// PreFlight concern, not a runtime crash.
    /// </summary>
    [JsonIgnore]
    public LibraryManifest CoreLibrary
    {
        get
        {
            var cores = LibraryManifests.Where(lib => lib.IsCoreLib).ToList();
            return cores.Count switch
            {
                1 => cores[0],
                0 => throw new InvalidOperationException(
                    "manifest.json is structurally invalid: library_manifests[] must contain exactly one entry with core_lib=true; found none."),
                _ => throw new InvalidOperationException(
                    $"manifest.json is structurally invalid: library_manifests[] must contain exactly one entry with core_lib=true; found {cores.Count} ({string.Join(", ", cores.Select(c => c.VcpkgName))})."),
            };
        }
    }
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
