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
