using System.Diagnostics.CodeAnalysis;

namespace Build.Domain.Versioning;

/// <summary>
/// Scope discriminator for <c>GitTagVersionProvider</c>.
/// <list type="bullet">
///   <item><see cref="Targeted"/> resolves a single family's tag at HEAD. The value may
///     be either a family id (<c>sdl2-core</c>) or a full family tag
///     (<c>sdl2-core-2.32.0</c>).</item>
///   <item><see cref="Train"/> resolves every concrete family tag at HEAD and orders
///     the result by dependency.</item>
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
