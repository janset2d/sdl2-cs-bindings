namespace Build.Versioning;

/// <summary>
/// Identifier for a package family declared in <c>build/manifest.json</c>'s
/// <c>package_families[]</c>. Format-agnostic non-empty string. Equality is
/// ordinal-exact; the manifest convention is canonical-lowercase, so case
/// drift in input is treated as a different family.
/// </summary>
public sealed record PackageFamilyId(string Value)
{
    public string Value { get; init; } = Validate(Value);

    public override string ToString() => Value;

    public static implicit operator string(PackageFamilyId id) => id.Value;

    private static string Validate(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value;
    }
}
