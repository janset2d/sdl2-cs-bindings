namespace Build.Validation.Models;

/// <summary>
/// Outcome of a single cross-family dependency resolvability check (release-guardrails [G58]).
/// Each satellite family's <c>depends_on</c> entry produces one check; the satellite is
/// "resolvable" iff its declared dependency can be satisfied either from within the current
/// scope (the in-flight <c>PackageFamilyVersionSet</c>) or — Pack stage only with
/// <c>--feed</c> — from a target feed.
/// </summary>
/// <remarks>
/// The current validator returns <see cref="InScope"/> and <see cref="Missing"/> only.
/// The feed-probe states (<see cref="OnFeed"/>, <see cref="FeedProbeFailed"/>) are reserved
/// for callers that choose to extend the validator with external-feed inspection.
/// </remarks>
public enum CrossFamilyDependencyCheckStatus
{
    /// <summary>Dependency family is present in the current invocation's version set.</summary>
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

/// <summary>
/// Aggregate of per-dependency cross-family checks emitted by
/// <c>ICrossFamilyDependencyResolvabilityValidator.Validate</c>. Release-guardrails [G58].
/// </summary>
public sealed record CrossFamilyDependencyValidation(IReadOnlyList<CrossFamilyDependencyCheck> Checks)
{
    public bool HasErrors => Checks.Any(check => check.IsError);
}
