using System.Text.Json.Serialization;

namespace Build.Data.BindingGeneration.Models;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record OpaqueHandleEntry
{
    [JsonPropertyName("name")] public required string Name { get; init; }
    [JsonPropertyName("header")] public string? Header { get; init; }
    [JsonPropertyName("header_decl")] public string? HeaderDecl { get; init; }
    [JsonPropertyName("header_note")] public string? HeaderNote { get; init; }
    [JsonPropertyName("wiki_url")] public string? WikiUrl { get; init; }
    [JsonPropertyName("wiki_evidence")] public string? WikiEvidence { get; init; }
    [JsonPropertyName("wiki_corroboration")] public string? WikiCorroboration { get; init; }
    [JsonPropertyName("reason")] public string? Reason { get; init; }
}
