using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Build.Data.Manifest.Models;
public record PrimaryBinary
{
    [JsonPropertyName("os")] public required string Os { get; init; }
    [JsonPropertyName("patterns")] public required IImmutableList<string> Patterns { get; init; }
}
