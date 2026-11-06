using System.Text.Json.Serialization;

namespace Build.Data.Manifest.Models;

public record RuntimeInfo
{
    [JsonPropertyName("rid")] public required string Rid { get; init; }
    [JsonPropertyName("triplet")] public required string Triplet { get; init; }
    [JsonPropertyName("runner")] public required string Runner { get; init; }
    [JsonPropertyName("container_image")] public string? ContainerImage { get; init; }
}
