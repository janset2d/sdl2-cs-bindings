using System.Text.Json.Serialization;

namespace Build.Data.Harvest;

public record DivergentLicense
{
    [JsonPropertyName("package")]
    public required string Package { get; init; }

    [JsonPropertyName("file_name")]
    public required string FileName { get; init; }

    [JsonPropertyName("rids")]
    public required IReadOnlyList<string> Rids { get; init; }
}
