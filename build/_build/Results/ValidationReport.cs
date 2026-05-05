using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace Build.Results;

/// <summary>
/// Severity classification for <see cref="ValidationCheck"/>. Two levels: warnings
/// surface concerns without failing; errors fail validation.
/// </summary>
public enum ValidationSeverity
{
    Warning,
    Error,
}

/// <summary>
/// Single finding emitted by a validator. <see cref="Name"/> describes what the
/// rule enforces in plain words (e.g. "Cross-family dependency resolvability");
/// <see cref="Code"/> is optional reporting metadata such as a release-guardrail
/// identifier (e.g. "G58") used by logs and reports. Construct via the public
/// ctor; <see cref="Name"/> and <see cref="Message"/> must be non-empty.
/// </summary>
public sealed record ValidationCheck(string Name, ValidationSeverity Severity, string Message, string? Code = null)
{
    public string Name { get; init; } = ValidateNonEmpty(Name);
    public string Message { get; init; } = ValidateNonEmpty(Message);

    private static string ValidateNonEmpty(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value;
    }
}

/// <summary>
/// Multi-check validation output. Immutable; aggregate per-validator reports through
/// <see cref="Combine(ValidationReport[])"/>. The report itself never throws — the
/// consuming task class translates an invalid report into a <c>CakeException</c> at
/// the task boundary so failure logging stays in one place.
/// </summary>
[SuppressMessage("Naming", "CA1710:Identifiers should have correct suffix",
    Justification = "The type's identity is 'a report of validation findings' — its public surface (IsValid, HasWarnings, Errors/Warnings projections, Combine) reads as report behavior, not collection operations. CA1710's suggested suffixes (Collection/Set/etc.) describe data-structure shape and would obscure that role.")]
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
