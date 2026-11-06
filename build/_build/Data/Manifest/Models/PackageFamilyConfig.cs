using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Build.Data.Manifest.Models;

public sealed record PackageFamilyConfig
{
    [JsonPropertyName("name")] public required string Name { get; init; }
    [JsonPropertyName("tag_prefix")] public required string TagPrefix { get; init; }
    [JsonPropertyName("managed_project")] public string? ManagedProject { get; init; }
    [JsonPropertyName("native_project")] public string? NativeProject { get; init; }
    [JsonPropertyName("library_ref")] public required string LibraryRef { get; init; }
    [JsonPropertyName("depends_on")] public required IImmutableList<string> DependsOn { get; init; }
    [JsonPropertyName("change_paths")] public required IImmutableList<string> ChangePaths { get; init; }
}
