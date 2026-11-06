using System.Text.Json.Serialization;

namespace Build.Data.Harvest;

/// <summary>
/// Represents the status of a harvest operation for a specific library and RID combination.
/// This is used in CI matrix jobs where each job handles one library+RID combination.
/// </summary>
public record RidHarvestStatus
{
    [JsonPropertyName("library_name")]
    public required string LibraryName { get; init; }

    [JsonPropertyName("rid")]
    public required string Rid { get; init; }

    [JsonPropertyName("triplet")]
    public required string Triplet { get; init; }

    [JsonPropertyName("success")]
    public required bool Success { get; init; }

    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; init; }

    [JsonPropertyName("timestamp")]
    public required DateTimeOffset Timestamp { get; init; }

    [JsonPropertyName("statistics")]
    public HarvestStatistics? Statistics { get; init; }
}
