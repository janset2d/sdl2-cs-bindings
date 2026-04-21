using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Build.Domain.Ci.Models;

/// <summary>
/// GitHub-Actions-shape matrix payload emitted by <c>GenerateMatrixTask</c> into
/// <c>artifacts/matrix/runtimes.json</c>. The <c>include</c> list is consumed by
/// <c>release.yml</c>'s <c>harvest</c> and <c>native-smoke</c> jobs via
/// <c>${{ fromJson(needs.generate-matrix.outputs.matrix) }}</c>.
/// </summary>
public sealed record MatrixOutput
{
    [JsonPropertyName("include")]
    public required IImmutableList<MatrixEntry> Include { get; init; }
}

public sealed record MatrixEntry
{
    [JsonPropertyName("rid")]
    public required string Rid { get; init; }

    [JsonPropertyName("triplet")]
    public required string Triplet { get; init; }

    [JsonPropertyName("strategy")]
    public required string Strategy { get; init; }

    [JsonPropertyName("runner")]
    public required string Runner { get; init; }

    [JsonPropertyName("container_image")]
    public string? ContainerImage { get; init; }
}
