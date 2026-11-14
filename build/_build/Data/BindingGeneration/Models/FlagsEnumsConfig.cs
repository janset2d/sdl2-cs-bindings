using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Build.Data.BindingGeneration.Models;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record FlagsEnumsConfig
{
    [JsonPropertyName("last_audited")] public string? LastAudited { get; init; }
    [JsonPropertyName("allow_list")] public ImmutableList<FlagsEnumEntry> AllowList { get; init; } = [];
}
