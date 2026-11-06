using System.Text.Json.Serialization;

namespace Build.Data.BindingGeneration.Models;

public sealed record ManualMacroConstantPolicyEntry
{
    [JsonPropertyName("reason")] public required string Reason { get; init; }
    [JsonPropertyName("allow_stale")] public bool AllowStale { get; init; }
}
