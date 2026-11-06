using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Build.Data.Manifest.Models;

public sealed record LinuxSystemArtefacts
{
    [JsonPropertyName("system_libraries")] public ImmutableList<string> SystemLibraries { get; init; } = [];
}
