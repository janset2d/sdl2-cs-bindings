namespace Build.Results;
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
