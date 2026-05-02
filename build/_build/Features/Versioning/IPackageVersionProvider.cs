using Build.Shared.Manifest;
using NuGet.Versioning;

namespace Build.Features.Versioning;

/// <summary>
/// Resolves a per-family version mapping from a source such as operator-supplied
/// values, manifest metadata, or git tags.
/// <para>
/// The returned mapping is the authoritative scope for the caller. Stage tasks
/// consume the mapping they are given and do not re-resolve versions internally.
/// Providers may accept a <paramref name="requestedScope"/> filter, but the final
/// scope is defined by the mapping that comes back.
/// </para>
/// <para>
/// When serialized by <c>ResolveVersions</c>, the mapping becomes a flat JSON object
/// keyed by family identifier with NuGet-normalized version strings as values. That
/// keeps the CLI, JSON, and in-memory dictionary forms aligned.
/// </para>
/// </summary>
public interface IPackageVersionProvider
{
    /// <summary>
    /// Resolves the per-family version mapping. Family identifier keys follow the
    /// <c>sdl&lt;major&gt;-&lt;role&gt;</c> canonical form (e.g., <c>sdl2-core</c>,
    /// <c>sdl2-image</c>) defined by <c>FamilyIdentifierConventions</c> and
    /// <see cref="ManifestConfig.PackageFamilies"/>.
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
