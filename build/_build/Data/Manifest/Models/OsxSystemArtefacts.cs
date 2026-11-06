using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Build.Data.Manifest.Models;

public record OsxSystemArtefacts
{
    [JsonPropertyName("system_libraries")] public ImmutableList<string> SystemLibraries { get; init; } = [];
}
