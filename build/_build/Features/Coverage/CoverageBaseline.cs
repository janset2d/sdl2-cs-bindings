using System.Text.Json.Serialization;

namespace Build.Features.Coverage;

/// <summary>
/// Coverage ratchet baseline — the static floor committed in <c>build/coverage-baseline.json</c>.
/// Thresholds are stored as percentages (0.0..100.0): the format humans raise by hand.
/// Optional metadata (<see cref="ReviewedAt"/>, <see cref="MeasuredLine"/>,
/// <see cref="MeasuredBranch"/>, <see cref="Notes"/>) is informational and does not
/// participate in the gate comparison.
/// </summary>
/// <remarks>
/// Deserialized directly from JSON; <c>required</c> guarantees the two floor fields exist,
/// and <see cref="JsonPropertyNameAttribute"/> maps snake_case JSON onto PascalCase members.
/// </remarks>
public sealed record CoverageBaseline
{
    /// <summary>Minimum acceptable line coverage percentage (0.0..100.0).</summary>
    [JsonPropertyName("line_coverage_min")]
    public required double LineCoverageMin { get; init; }

    /// <summary>Minimum acceptable branch coverage percentage (0.0..100.0).</summary>
    [JsonPropertyName("branch_coverage_min")]
    public required double BranchCoverageMin { get; init; }

    /// <summary>Date the floor was last reviewed (informational).</summary>
    [JsonPropertyName("reviewed_at")]
    public string? ReviewedAt { get; init; }

    /// <summary>Line percentage at the time the floor was set (informational).</summary>
    [JsonPropertyName("measured_line")]
    public double? MeasuredLine { get; init; }

    /// <summary>Branch percentage at the time the floor was set (informational).</summary>
    [JsonPropertyName("measured_branch")]
    public double? MeasuredBranch { get; init; }

    /// <summary>Human-readable note explaining the floor revision (informational).</summary>
    [JsonPropertyName("notes")]
    public string? Notes { get; init; }
}
