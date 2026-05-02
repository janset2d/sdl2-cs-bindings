using Build.Shared.Manifest;

namespace Build.Features.Harvesting;

public interface IBinaryClosureWalker
{
    Task<ClosureResult> BuildClosureAsync(LibraryManifest manifest, CancellationToken ct = default);
}
