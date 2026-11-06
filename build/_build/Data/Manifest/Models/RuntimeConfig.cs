using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Build.Data.Manifest.Models;
public record RuntimeConfig
{
    [JsonPropertyName("runtimes")] public required IImmutableList<RuntimeInfo> Runtimes { get; init; }
}
