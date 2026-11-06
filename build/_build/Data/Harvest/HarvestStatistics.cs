using System.Text.Json.Serialization;

namespace Build.Data.Harvest;

/// <summary>
/// Summary statistics from a harvest operation.
/// </summary>
public sealed record HarvestStatistics
{
    [JsonPropertyName("primary_files_count")]
    public required int PrimaryFilesCount { get; init; }

    [JsonPropertyName("runtime_files_count")]
    public required int RuntimeFilesCount { get; init; }

    [JsonPropertyName("license_files_count")]
    public required int LicenseFilesCount { get; init; }

    [JsonPropertyName("deployed_packages_count")]
    public required int DeployedPackagesCount { get; init; }

    [JsonPropertyName("filtered_packages_count")]
    public required int FilteredPackagesCount { get; init; }

    [JsonPropertyName("deployment_strategy")]
    public required string DeploymentStrategy { get; init; }
}
