using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Build.Data.BindingGeneration.Models;
public sealed record HeaderSetConfig
{
    [JsonPropertyName("include_dir_glob")] public required string IncludeDirGlob { get; init; }
    [JsonPropertyName("header_glob")] public required string HeaderGlob { get; init; }
    [JsonPropertyName("excluded_headers")] public ImmutableList<string> ExcludedHeaders { get; init; } = [];
    [JsonPropertyName("excluded_header_prefixes")] public ImmutableList<string> ExcludedHeaderPrefixes { get; init; } = [];
}
