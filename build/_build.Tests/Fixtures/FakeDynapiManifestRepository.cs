using Build.Data.BindingGeneration;
using Build.Data.BindingGeneration.Models;
using Build.Results;

namespace Build.Tests.Fixtures;
/// <summary>
/// Synchronously-returning fake <see cref="IDynapiManifestRepository"/> for
/// validator tests. Avoids the round-trip-test ceremony of seeding a real
/// vcpkg buildtree fake filesystem — validators only care about the
/// resolved <see cref="DynapiManifest"/> payload.
/// </summary>
public sealed class FakeDynapiManifestRepository(IReadOnlySet<string>? publicSymbols, ManifestResolutionError? error = null) : IDynapiManifestRepository
{
    private readonly IReadOnlySet<string> _publicSymbols = publicSymbols ?? new HashSet<string>(StringComparer.Ordinal);
    private readonly ManifestResolutionError? _error = error;

    public Task<Result<DynapiManifest, ManifestResolutionError>> LoadAsync(CancellationToken ct = default)
    {
        if (_error is not null)
        {
            return Task.FromResult(Result<DynapiManifest, ManifestResolutionError>.Failure(_error));
        }

        var manifest = new DynapiManifest(
            PublicSymbols: _publicSymbols,
            Origin: DynapiManifestOrigin.VcpkgBuildtree,
            SourcePath: "/fake/SDL2.exports");
        return Task.FromResult(Result<DynapiManifest, ManifestResolutionError>.Success(manifest));
    }
}
