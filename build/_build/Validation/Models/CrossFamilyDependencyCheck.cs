
namespace Build.Validation.Models;
/// <summary>
/// One cross-family dependency check: satellite family → declared dependency family.
/// Release-guardrails [G58].
/// </summary>
/// <param name="DependentFamily">The family with a <c>depends_on</c> entry that is being
/// validated (e.g., <c>sdl2-image</c>).</param>
/// <param name="DependencyFamily">The declared dependency family (e.g., <c>sdl2-core</c>).</param>
/// <param name="ExpectedMinVersion">The minimum-range lower bound the satellite will emit
/// in its <c>.nuspec</c> dependency entry — this equals the satellite's own resolved
/// version per SkiaSharp-style within-family orchestration.</param>
/// <param name="Status">Outcome of the resolvability check.</param>
/// <param name="ErrorMessage">Human-readable explanation when
/// <see cref="IsError"/> is <see langword="true"/>.</param>
public sealed record CrossFamilyDependencyCheck(
    string DependentFamily,
    string DependencyFamily,
    string ExpectedMinVersion,
    CrossFamilyDependencyCheckStatus Status,
    string? ErrorMessage)
{
    public bool IsError => Status is CrossFamilyDependencyCheckStatus.Missing or CrossFamilyDependencyCheckStatus.FeedProbeFailed;
}
