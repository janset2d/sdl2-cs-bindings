using NuGet.Versioning;

namespace Build.Application.Versioning;

/// <summary>
/// Resolves a per-family version mapping from some source (operator-supplied, manifest, git tag).
/// Introduced as the ADR-003 §3.1 version-provider seam. Three implementations are planned:
/// <list type="bullet">
///   <item><see cref="ExplicitVersionProvider"/> (Slice A) — operator-supplied mapping validated against manifest.</item>
///   <item><c>ManifestVersionProvider</c> (Slice B1) — manifest upstream + suffix composition.</item>
///   <item><c>GitTagVersionProvider</c> (Slice C) — family-tag / meta-tag discovery with topological ordering.</item>
/// </list>
/// <para>
/// <b>Resolve-once invariant (ADR-003 §2.4):</b> within a single invocation the resolved mapping
/// is immutable. Stage tasks consume the mapping; they do not re-resolve it. The CI job graph
/// realizes this via a dedicated <c>ResolveVersions</c> job whose output is piped to downstream
/// jobs through <c>needs:</c>. The local-dev path realizes it via
/// <c>LocalArtifactSourceResolver.PrepareFeedAsync</c> resolving once and passing the same
/// mapping instance to every stage runner it composes.
/// </para>
/// <para>
/// <b>Single scope axis (ADR-003 §2.2):</b> the mapping's key set IS the scope. Stage tasks
/// never receive a separate family list alongside the mapping. Providers may accept a
/// <paramref name="requestedScope"/> filter so CI-side callers can carve out a subset without
/// rebuilding the mapping, but the scope's authoritative home is the resolved mapping itself.
/// </para>
/// <para>
/// <b>JSON output shape (canonical contract, locked at plan-lock):</b> when the mapping is
/// serialized by the <c>ResolveVersions</c> task for CI <c>needs:</c> consumption, it is a
/// flat object keyed by family identifier with NuGet-normalized version strings as values:
/// <code>
/// {
///   "sdl2-core": "2.32.0-ci.run-id-12345",
///   "sdl2-image": "2.8.0-ci.run-id-12345"
/// }
/// </code>
/// This shape maps 1:1 to the provider's in-memory <see cref="IReadOnlyDictionary{TKey, TValue}"/>
/// return type, to the <c>--explicit-version</c> CLI repeated-option input
/// (<c>--explicit-version sdl2-core=2.32.0-ci.run-id-12345</c>), and to YAML
/// <c>${{ fromJson(needs.resolve-versions.outputs.versions-json) }}</c> consumption — no
/// intermediate parsing.
/// </para>
/// </summary>
public interface IPackageVersionProvider
{
    /// <summary>
    /// Resolves the per-family version mapping. Family identifier keys follow the
    /// <c>sdl&lt;major&gt;-&lt;role&gt;</c> canonical form (e.g., <c>sdl2-core</c>,
    /// <c>sdl2-image</c>) defined by <c>FamilyIdentifierConventions</c> and
    /// <see cref="Context.Models.ManifestConfig.PackageFamilies"/>.
    /// </summary>
    /// <param name="requestedScope">
    /// Optional caller-supplied scope filter. When empty, the provider returns every mapping
    /// entry it owns. When non-empty, the returned mapping is the intersection of the provider's
    /// known entries and the requested keys; missing keys surface as a provider-specific error.
    /// Scope is matched case-insensitively on family identifier per repo convention.
    /// </param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<IReadOnlyDictionary<string, NuGetVersion>> ResolveAsync(
        IReadOnlySet<string> requestedScope,
        CancellationToken cancellationToken = default);
}
