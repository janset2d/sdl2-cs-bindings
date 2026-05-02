namespace Build.Features.Coverage;

/// <summary>
/// Parsed metrics from a cobertura coverage report.
/// Rates are in the 0.0..1.0 range (as emitted by cobertura); percentages are derived.
/// </summary>
public sealed record CoverageMetrics
{
    /// <summary>Line coverage as a ratio (0.0..1.0).</summary>
    public required double LineRate { get; init; }

    /// <summary>Branch coverage as a ratio (0.0..1.0).</summary>
    public required double BranchRate { get; init; }

    public required int LinesCovered { get; init; }
    public required int LinesValid { get; init; }
    public required int BranchesCovered { get; init; }
    public required int BranchesValid { get; init; }

    /// <summary>Line coverage as a percentage (0.0..100.0).</summary>
    public double LinePercent => LineRate * 100.0;

    /// <summary>Branch coverage as a percentage (0.0..100.0).</summary>
    public double BranchPercent => BranchRate * 100.0;
}
