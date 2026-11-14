using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Build.Data.BindingGeneration.Models;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HeaderSetConfig
{
    [JsonPropertyName("include_dir")] public required string IncludeDir { get; init; }
    [JsonPropertyName("headers")] public required ImmutableList<HeaderEntryConfig> Headers { get; init; }
}
