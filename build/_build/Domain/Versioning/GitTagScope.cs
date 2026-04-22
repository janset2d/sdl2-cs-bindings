using System.Diagnostics.CodeAnalysis;

namespace Build.Domain.Versioning;

/// <summary>
/// Scope discriminator for <c>GitTagVersionProvider</c> per ADR-003 §3.1. Two modes that
/// mirror the release vocabulary documented in
/// <c>docs/knowledge-base/release-lifecycle-direction.md</c>:
/// <list type="bullet">
///   <item><see cref="Targeted"/> — single-family release (e.g., push of
///     <c>sdl2-image-2.8.1</c>). Provider reads the single <c>{tag_prefix}-{semver}</c>
///     tag pointing at HEAD for that family.</item>
///   <item><see cref="Train"/> — full-train / coordinated multi-family release. Provider
///     walks every concrete family in <c>manifest.package_families[]</c>, discovers each
///     family's <c>{tag_prefix}-{semver}</c> tag at HEAD, and topologically orders by
///     <c>depends_on</c>. Per-family filtering goes through
///     <c>IPackageVersionProvider.ResolveAsync</c>'s <c>requestedScope</c>.</item>
/// </list>
/// </summary>
/// <remarks>
/// Both modes anchor at HEAD. For release trigger flows this is sound: tag push checks
/// out the tag's commit, <c>GitLogTip</c> returns that commit, and per-family tags at
/// that commit resolve directly. Operator-driven ad-hoc runs against a non-release commit
/// surface as "no tag at HEAD for family X" with an actionable error message.
/// </remarks>
public abstract record GitTagScope
{
    [SuppressMessage("Design", "CA1034:Nested types should not be visible",
        Justification = "Sum-type ADT pattern: nested cases keep the discriminated-union DSL ergonomic (GitTagScope.Targeted / GitTagScope.Train) without polluting the namespace with GitTagTargetedScope / GitTagTrainScope noise.")]
    public sealed record Targeted(string FamilyId) : GitTagScope;

    [SuppressMessage("Design", "CA1034:Nested types should not be visible",
        Justification = "Sum-type ADT pattern: see Targeted.")]
    public sealed record Train : GitTagScope;
}
