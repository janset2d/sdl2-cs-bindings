using Build.Domain.Coverage.Models;
using Build.Domain.Results;

namespace Build.Domain.Coverage.Results;

/// <summary>
/// Base class for coverage ratchet errors. Mirrors the <c>StrategyError</c> / <c>HarvestingError</c>
/// hierarchy used elsewhere in the build host.
/// </summary>
public abstract class CoverageError : BuildError
{
    protected CoverageError(string message, Exception? exception = null)
        : base(message, exception)
    {
    }
}

/// <summary>
/// The measured coverage dropped below the baseline floor for at least one metric.
/// Carries the full context (measured metrics, configured baseline, per-metric failure
/// descriptions) so callers can log actionable remediation output.
/// </summary>
public sealed class CoverageThresholdViolation : CoverageError
{
    public CoverageMetrics Metrics { get; }

    public CoverageBaseline Baseline { get; }

    public IReadOnlyList<string> Failures { get; }

    public CoverageThresholdViolation(CoverageMetrics metrics, CoverageBaseline baseline, IReadOnlyList<string> failures, Exception? exception = null)
        : base(BuildMessage(failures), exception)
    {
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(failures);

        Metrics = metrics;
        Baseline = baseline;
        Failures = [.. failures]; // Defensive copy
    }

    private static string BuildMessage(IReadOnlyList<string> failures)
    {
        ArgumentNullException.ThrowIfNull(failures);
        return failures.Count switch
        {
            0 => "Coverage ratchet violation (no failure details provided).",
            1 => failures[0],
            _ => $"Coverage ratchet violation: {failures.Count} metric(s) below floor.",
        };
    }
}
