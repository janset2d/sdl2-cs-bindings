namespace Build.Validation.Models;

public sealed record UpstreamVersionAlignmentCheck(
    string FamilyIdentifier,
    string? LibraryRef,
    string FamilyVersion,
    string? UpstreamVersion,
    UpstreamVersionAlignmentCheckStatus Status,
    string? ErrorMessage)
{
    public bool IsError => Status is not UpstreamVersionAlignmentCheckStatus.Match;
}
