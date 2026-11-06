using System.Text.Json.Serialization;

namespace Build.Targets.GenerateMatrix.Models;
public sealed record MatrixEntry
{
    [JsonPropertyName("rid")]
    public required string Rid { get; init; }

    [JsonPropertyName("triplet")]
    public required string Triplet { get; init; }

    [JsonPropertyName("runner")]
    public required string Runner { get; init; }

    [JsonPropertyName("container_image")]
    public string? ContainerImage { get; init; }
}
