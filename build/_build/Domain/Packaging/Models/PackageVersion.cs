namespace Build.Domain.Packaging.Models;

/// <summary>
/// A normalized NuGet SemVer family version resolved from CLI input (and, in future,
/// MinVer-derived git tags). Wrapping the raw string gives the Result-pattern
/// <c>PackageVersionResolutionResult</c> a typed success payload instead of <c>string</c>.
/// </summary>
public sealed record PackageVersion(string Value)
{
    public override string ToString() => Value;
}
