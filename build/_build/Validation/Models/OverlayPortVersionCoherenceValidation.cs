namespace Build.Validation.Models;

public sealed record OverlayPortVersionCoherenceValidation(IReadOnlyList<OverlayPortVersionCheck> Checks)
{
    public bool HasErrors => Checks.Any(c => c.Status != OverlayPortVersionCheckStatus.Match);
}
