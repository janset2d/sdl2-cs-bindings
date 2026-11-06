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
