using System.Text.Json.Serialization;

namespace Build.Data.BindingGeneration.Models;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RequiredFunctionParameter
{
    [JsonPropertyName("type")] public required string Type { get; init; }
    [JsonPropertyName("name")] public required string Name { get; init; }
}
