using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Build.Data.BindingGeneration.Models;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RequiredFunctionConfig
{
    [JsonPropertyName("name")] public required string Name { get; init; }
    [JsonPropertyName("return_type")] public required string ReturnType { get; init; }
    [JsonPropertyName("source_header")] public required string SourceHeader { get; init; }
    [JsonPropertyName("parameters")] public ImmutableList<RequiredFunctionParameter> Parameters { get; init; } = [];
}
