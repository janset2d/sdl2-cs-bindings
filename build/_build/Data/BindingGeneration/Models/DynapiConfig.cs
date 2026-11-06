using System.Text.Json.Serialization;

namespace Build.Data.BindingGeneration.Models;
public sealed record DynapiConfig
{
    [JsonPropertyName("exports_glob")] public required string ExportsGlob { get; init; }
}
