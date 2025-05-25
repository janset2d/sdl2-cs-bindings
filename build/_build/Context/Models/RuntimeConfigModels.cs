using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Build.Context.Models;

public record RuntimeInfo
{
    [JsonPropertyName("rid")] public required string Rid { get; init; }

    [JsonPropertyName("triplet")] public required string Triplet { get; init; }

    [JsonPropertyName("runner")] public required string Runner { get; init; }

    [JsonPropertyName("container_image")] public string? ContainerImage { get; init; }
}

public record RuntimeConfig
{
    [JsonPropertyName("runtimes")] public required IImmutableList<RuntimeInfo> Runtimes { get; init; }
}
