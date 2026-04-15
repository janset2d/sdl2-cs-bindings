using Build.Modules.Coverage.Models;

namespace Build.Modules.Coverage.Results;

/// <summary>
/// Represents a successful coverage ratchet check — both line and branch coverage met or
/// exceeded the configured baseline floor. Carries the measured metrics and the baseline
/// used for the comparison so callers can emit a coherent report.
/// </summary>
public sealed record CoverageCheckSuccess
{
    public CoverageMetrics Metrics { get; }

    public CoverageBaseline Baseline { get; }

    public CoverageCheckSuccess(CoverageMetrics metrics, CoverageBaseline baseline)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(baseline);

        Metrics = metrics;
        Baseline = baseline;
    }
}
