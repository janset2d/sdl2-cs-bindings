using System.Text.Json.Serialization;

namespace Build.Data.BindingGeneration.Models;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record HeaderEntryConfig
{
    [JsonPropertyName("order")] public required int Order { get; init; }
    [JsonPropertyName("name")] public required string Name { get; init; }
}
