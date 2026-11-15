using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Build.Data.BindingGeneration.Models;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BindingGenerationSettings
{
    [JsonPropertyName("base_rsp")] public required string BaseRsp { get; init; }
    [JsonPropertyName("per_header_rsp_dir")] public required string PerHeaderRspDir { get; init; }
    [JsonPropertyName("platform_header_shims_dir")] public required string PlatformHeaderShimsDir { get; init; }
    [JsonPropertyName("family_order")] public required ImmutableList<string> FamilyOrder { get; init; }
    [JsonPropertyName("platform_views")] public required ImmutableList<PlatformViewConfig> PlatformViews { get; init; }
    [JsonPropertyName("all_platform_macros")] public required ImmutableList<string> AllPlatformMacros { get; init; }
}
