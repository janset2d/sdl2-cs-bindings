using System.Text.Json.Serialization;

namespace Build.Data.BindingGeneration.Models;
public sealed record DeferredDeclarationConfig
{
    [JsonPropertyName("category")] public required string Category { get; init; }
    [JsonPropertyName("reason")] public required string Reason { get; init; }
}
