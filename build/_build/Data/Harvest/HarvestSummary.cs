using System.Text.Json.Serialization;

namespace Build.Data.Harvest;

/// <summary>
/// Summary information for the harvest manifest.
/// </summary>
public record HarvestSummary
{
    [JsonPropertyName("total_rids")]
    public required int TotalRids { get; init; }

    [JsonPropertyName("successful_rids")]
    public required int SuccessfulRids { get; init; }

    [JsonPropertyName("failed_rids")]
    public required int FailedRids { get; init; }

    [JsonPropertyName("success_rate")]
    public required double SuccessRate { get; init; }
}
