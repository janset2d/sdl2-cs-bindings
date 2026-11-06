using System.Text.Json.Serialization;

namespace Build.Data.BindingGeneration.Models;
public sealed record RequiredFunctionParameter
{
    [JsonPropertyName("type")] public required string Type { get; init; }
    [JsonPropertyName("name")] public required string Name { get; init; }
}
