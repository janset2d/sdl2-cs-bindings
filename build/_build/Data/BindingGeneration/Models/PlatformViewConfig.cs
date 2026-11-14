using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Build.Data.BindingGeneration.Models;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PlatformViewConfig
{
    [JsonPropertyName("name")] public required string Name { get; init; }
    [JsonPropertyName("supported_os")] public required string SupportedOs { get; init; }
    [JsonPropertyName("defines")] public required ImmutableList<string> Defines { get; init; }
}
