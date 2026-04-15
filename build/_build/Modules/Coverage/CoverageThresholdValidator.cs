using System.Globalization;
using Build.Modules.Coverage.Models;
using Build.Modules.Coverage.Results;

namespace Build.Modules.Coverage;

/// <summary>
/// Static floor comparison: measured percentage must be greater than or equal to the baseline
/// floor for both line and branch metrics. The baseline is configured at percentage scale
/// (0..100) while cobertura metrics are at ratio scale (0..1) — the comparison happens on
/// percentages via <see cref="CoverageMetrics.LinePercent"/> and <see cref="CoverageMetrics.BranchPercent"/>.
/// </summary>
internal static class CoverageThresholdValidator
{
    public static CoverageCheckResult Validate(CoverageMetrics metrics, CoverageBaseline baseline)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(baseline);

        var failures = new List<string>();

        if (metrics.LinePercent < baseline.LineCoverageMin)
        {
            failures.Add(string.Format(
                CultureInfo.InvariantCulture,
                "Line coverage {0:F2}% is below the minimum floor {1:F2}% (see build/coverage-baseline.json).",
                metrics.LinePercent,
                baseline.LineCoverageMin));
        }

        if (metrics.BranchPercent < baseline.BranchCoverageMin)
        {
            failures.Add(string.Format(
                CultureInfo.InvariantCulture,
                "Branch coverage {0:F2}% is below the minimum floor {1:F2}% (see build/coverage-baseline.json).",
                metrics.BranchPercent,
                baseline.BranchCoverageMin));
        }

        if (failures.Count == 0)
        {
            return new CoverageCheckSuccess(metrics, baseline);
        }

        return new CoverageThresholdViolation(metrics, baseline, failures);
    }
}
