using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace Build.Results;

/// <summary>
/// Multi-check validation output. Immutable; aggregate per-validator reports through
/// <see cref="Combine(ValidationReport[])"/>. The report itself never throws — the
/// consuming task class translates an invalid report into a <c>CakeException</c> at
/// the task boundary so failure logging stays in one place.
/// </summary>
public sealed class ValidationReport : IReadOnlyCollection<ValidationCheck>
{
    public static ValidationReport Empty { get; } = new([]);

    private readonly IReadOnlyList<ValidationCheck> _checks;

    public ValidationReport(IEnumerable<ValidationCheck> checks)
    {
        ArgumentNullException.ThrowIfNull(checks);

        _checks = [.. checks];
        Errors = [.. _checks.Where(c => c.Severity == ValidationSeverity.Error)];
        Warnings = [.. _checks.Where(c => c.Severity == ValidationSeverity.Warning)];
    }

    public int Count => _checks.Count;

    public bool IsValid => Errors.Count == 0;

    public bool HasWarnings => Warnings.Count > 0;

    public IReadOnlyList<ValidationCheck> Errors { get; }

    public IReadOnlyList<ValidationCheck> Warnings { get; }

    public static ValidationReport Combine(params ValidationReport[] reports)
    {
        ArgumentNullException.ThrowIfNull(reports);
        return new ValidationReport(reports.SelectMany(static r => r));
    }

    public IEnumerator<ValidationCheck> GetEnumerator() => _checks.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
