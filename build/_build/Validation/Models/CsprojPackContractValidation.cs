namespace Build.Validation.Models;

public sealed record CsprojPackContractValidation(IReadOnlyList<CsprojPackContractCheck> Checks)
{
    public bool HasErrors => Checks.Any(check => check.IsError);

    public int CheckedCsprojs => Checks
        .Where(c => !string.IsNullOrEmpty(c.CsprojRelativePath))
        .Select(c => c.CsprojRelativePath)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count();

    public int CheckedFamilies => Checks
        .Select(c => c.FamilyIdentifier)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count();
}
