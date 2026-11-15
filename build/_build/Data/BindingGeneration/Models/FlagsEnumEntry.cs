using System.Text.Json.Serialization;

namespace Build.Data.BindingGeneration.Models;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FlagsEnumEntry
{
    [JsonPropertyName("name")] public required string Name { get; init; }
    [JsonPropertyName("header")] public required string Header { get; init; }
    [JsonPropertyName("reason")] public required string Reason { get; init; }
}
