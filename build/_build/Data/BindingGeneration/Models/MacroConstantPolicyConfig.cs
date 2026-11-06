using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Build.Data.BindingGeneration.Models;

public sealed record MacroConstantPolicyConfig
{
    [JsonPropertyName("excluded")]
    public ImmutableDictionary<string, ManualMacroConstantPolicyEntry> Excluded { get; init; } =
        [];

    [JsonPropertyName("overrides")]
    public ImmutableDictionary<string, MacroConstantOverrideConfig> Overrides { get; init; } =
        [];
}
