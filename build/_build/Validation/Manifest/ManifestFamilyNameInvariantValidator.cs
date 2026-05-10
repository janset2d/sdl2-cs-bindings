using System.Text.RegularExpressions;
using Build.Manifest;
using Build.Results;

namespace Build.Validation.Manifest;

/// <summary>
/// Validates that every <c>package_families[].name</c> is lowercase kebab-case
/// matching the canonical <c>sdl&lt;major&gt;-&lt;role&gt;</c> pattern. Hand-edited
/// mixed-case entries (e.g. <c>SDL2-Core</c>) silently bypass <c>PackageFamilyId</c>
/// ordinal-exact lookups in downstream consumers; this validator catches the drift
/// at PreFlight time before any build operation runs.
/// </summary>
public sealed partial class ManifestFamilyNameInvariantValidator : IManifestFamilyNameInvariantValidator
{
    [GeneratedRegex(@"^sdl[0-9]+-[a-z][a-z0-9-]*$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex FamilyNamePattern();

    public ValidationReport Validate(ManifestConfig manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var checks = manifest.PackageFamilies
            .Select(family => Evaluate(family.Name))
            .OfType<ValidationCheck>()
            .ToList();

        return checks.Count == 0 ? ValidationReport.Empty : new ValidationReport(checks);
    }

    private static ValidationCheck? Evaluate(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return new ValidationCheck(
                Name: "Manifest family name invariant",
                Severity: ValidationSeverity.Error,
                Message: "Manifest package_families[] entry has an empty name. Every family must declare a 'sdl<major>-<role>' kebab-case identifier.",
                Code: "G59");
        }

        if (!FamilyNamePattern().IsMatch(name))
        {
            return new ValidationCheck(
                Name: "Manifest family name invariant",
                Severity: ValidationSeverity.Error,
                Message: $"Family name '{name}' does not match required pattern 'sdl<major>-<role>' (lowercase kebab, e.g. 'sdl2-core', 'sdl2-image'). Mixed-case or underscore-delimited names break PackageFamilyId ordinal-exact lookups in downstream consumers.",
                Code: "G59");
        }

        return null;
    }
}
