
namespace Build.Validation.Models;
public sealed record UpstreamVersionAlignmentValidation(IReadOnlyList<UpstreamVersionAlignmentCheck> Checks)
{
    public bool HasErrors => Checks.Any(check => check.IsError);

    // Manifest-shape errors (duplicate names) short-circuit the validator before any per-family
    // alignment runs, so they do not represent a "checked family". Excluding them here keeps
    // the reporter's "{0} family row(s) evaluated" message accurate when the only entries are
    // the duplicate-detection rows.
    public int CheckedFamilies => Checks
        .Where(check => check.Status is not UpstreamVersionAlignmentCheckStatus.DuplicateFamilyName
                                      and not UpstreamVersionAlignmentCheckStatus.DuplicateLibraryName)
        .Select(check => check.FamilyIdentifier)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count();
}
