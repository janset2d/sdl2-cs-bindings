using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Build.Data.Manifest.Models;
public record ManifestConfig
{
    [JsonPropertyName("schema_version")] public required string SchemaVersion { get; init; }
    [JsonPropertyName("packaging_config")] public required PackagingConfig PackagingConfig { get; init; }
    [JsonPropertyName("runtimes")] public required IImmutableList<RuntimeInfo> Runtimes { get; init; }
    [JsonPropertyName("package_families")] public required IImmutableList<PackageFamilyConfig> PackageFamilies { get; init; }
    [JsonPropertyName("system_exclusions")] public required SystemArtefactsConfig SystemExclusions { get; init; }
    [JsonPropertyName("library_manifests")] public required IImmutableList<LibraryManifest> LibraryManifests { get; init; }

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
                    "manifest.json is structurally invalid: library_manifests[] must contain exactly one entry with core_lib=true; found " +
                    $"{cores.Count} ({string.Join(", ", cores.Select(c => c.VcpkgName))})."),
            };
        }
    }
}
