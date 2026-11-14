using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Build.Data.BindingGeneration.Models;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record OpaqueHandlesConfig
{
    [JsonPropertyName("last_audited")] public string? LastAudited { get; init; }
    [JsonPropertyName("source")] public string? Source { get; init; }
    [JsonPropertyName("auto_detect_well_known")] public ImmutableList<OpaqueHandleEntry> AutoDetectWellKnown { get; init; } = [];
    [JsonPropertyName("force_opaque_exceptions")] public ImmutableList<OpaqueHandleEntry> ForceOpaqueExceptions { get; init; } = [];
    [JsonPropertyName("excluded_candidates")] public ImmutableList<OpaqueHandleEntry> ExcludedCandidates { get; init; } = [];
}
