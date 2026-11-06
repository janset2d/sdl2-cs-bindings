using System.Text.Json.Serialization;

namespace Build.Data.BindingGeneration.Models;
public sealed record MacroConstantOverrideConfig
{
    [JsonPropertyName("type")] public required string Type { get; init; }
    [JsonPropertyName("value")] public required string Value { get; init; }
    [JsonPropertyName("source_header")] public required string SourceHeader { get; init; }
    [JsonPropertyName("kind")] public required ConstantKind Kind { get; init; }
    [JsonPropertyName("reason")] public required string Reason { get; init; }
    [JsonPropertyName("allow_stale")] public bool AllowStale { get; init; }
}
