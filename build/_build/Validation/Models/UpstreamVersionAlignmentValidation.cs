namespace Build.Validation.Models;

public sealed record UpstreamVersionAlignmentValidation(IReadOnlyList<UpstreamVersionAlignmentCheck> Checks)
{
    public bool HasErrors => Checks.Any(check => check.IsError);

    /// <summary>Excludes manifest-shape failures that precede family validation.</summary>
    public int CheckedFamilies => Checks
        .Where(check => check.Status is not UpstreamVersionAlignmentCheckStatus.DuplicateFamilyName
                                      and not UpstreamVersionAlignmentCheckStatus.DuplicateLibraryName)
        .Select(check => check.FamilyIdentifier)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count();
}
