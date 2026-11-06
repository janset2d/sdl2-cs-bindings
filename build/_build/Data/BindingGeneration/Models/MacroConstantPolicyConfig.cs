using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Build.Data.BindingGeneration.Models;
public sealed record MacroConstantPolicyConfig
{
    [JsonPropertyName("excluded")]
    public ImmutableDictionary<string, ManualMacroConstantPolicyEntry> Excluded { get; init; } =
        ImmutableDictionary<string, ManualMacroConstantPolicyEntry>.Empty;

    [JsonPropertyName("overrides")]
    public ImmutableDictionary<string, MacroConstantOverrideConfig> Overrides { get; init; } =
        ImmutableDictionary<string, MacroConstantOverrideConfig>.Empty;
}
