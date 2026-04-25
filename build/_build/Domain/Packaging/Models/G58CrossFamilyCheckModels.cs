namespace Build.Domain.Packaging.Models;

/// <summary>
/// Outcome of a single G58 cross-family dependency resolvability check. Each satellite
/// family's <c>depends_on</c> entry produces one check; the satellite is "resolvable" iff
/// its declared dependency can be satisfied either from within the current scope
/// (same <c>IPackageVersionProvider</c> mapping) or — Pack stage only with
/// <c>--feed</c> — from a target feed.
/// </summary>
/// <remarks>
/// The current validator returns <see cref="InScope"/> and <see cref="Missing"/> only.
/// The feed-probe states (<see cref="OnFeed"/>, <see cref="FeedProbeFailed"/>) are reserved
/// for callers that choose to extend G58 with external-feed inspection.
/// </remarks>
public enum G58CrossFamilyCheckStatus
{
    /// <summary>Dependency family is present in the current invocation's version mapping.</summary>
    InScope,

    /// <summary>
    /// Dependency family is not in scope AND (when feed-probe is unavailable or negative)
    /// cannot be satisfied. Fail the invocation.
    /// </summary>
    Missing,

    /// <summary>
    /// Dependency family is not in scope, but the target feed has a published package at a
    /// version satisfying the satellite's declared minimum range. Reserved for Pack-stage
    /// feed-probe path.
    /// </summary>
    OnFeed,

    /// <summary>
    /// Dependency family is not in scope and feed-probe was requested but could not complete
    /// (network error, auth failure, etc.). Surfaced separately from <see cref="Missing"/>
    /// so the operator can distinguish "truly unresolvable" from "probe unavailable".
    /// Reserved for Pack-stage feed-probe path.
    /// </summary>
    FeedProbeFailed,
}

/// <summary>
/// One G58 cross-family dependency check: satellite family → declared dependency family.
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
public sealed record G58CrossFamilyCheck(
    string DependentFamily,
    string DependencyFamily,
    string ExpectedMinVersion,
    G58CrossFamilyCheckStatus Status,
    string? ErrorMessage)
{
    public bool IsError => Status is G58CrossFamilyCheckStatus.Missing or G58CrossFamilyCheckStatus.FeedProbeFailed;
}

/// <summary>
/// Aggregate of per-dependency G58 checks emitted by
/// <c>IG58CrossFamilyDepResolvabilityValidator.Validate</c>.
/// </summary>
public sealed record G58CrossFamilyValidation(IReadOnlyList<G58CrossFamilyCheck> Checks)
{
    public bool HasErrors => Checks.Any(check => check.IsError);
}
