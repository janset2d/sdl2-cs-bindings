namespace Build.Context.Configs;

/// <summary>
/// Holds the ADR-003 version-axis CLI inputs for the <c>ResolveVersions</c> target. Stage
/// tasks (PreFlight / Package / ConsumerSmoke) do NOT read this configuration — they consume
/// the resolved mapping through <c>IPackageVersionProvider</c>. This class carries only the
/// inputs that shape the <c>ResolveVersions</c> invocation itself.
/// </summary>
public sealed class VersioningConfiguration(string? versionSource, string? suffix, IReadOnlyList<string> scope)
{
    /// <summary>
    /// Raw <c>--version-source</c> value (<c>manifest</c>, <c>explicit</c>, <c>git-tag</c>,
    /// <c>meta-tag</c>). Null when the option was not supplied; the <c>ResolveVersions</c>
    /// runner validates this on entry.
    /// </summary>
    public string? VersionSource { get; } = string.IsNullOrWhiteSpace(versionSource) ? null : versionSource.Trim();

    /// <summary>
    /// Raw <c>--suffix</c> value. Required when <see cref="VersionSource"/> is <c>manifest</c>.
    /// </summary>
    public string? Suffix { get; } = string.IsNullOrWhiteSpace(suffix) ? null : suffix.Trim();

    /// <summary>
    /// Repeated <c>--scope</c> entries. Empty list means "all families in manifest" for
    /// <see cref="VersionSource"/> <c>manifest</c>.
    /// </summary>
    public IReadOnlyList<string> Scope { get; } = scope ?? throw new ArgumentNullException(nameof(scope));
}
