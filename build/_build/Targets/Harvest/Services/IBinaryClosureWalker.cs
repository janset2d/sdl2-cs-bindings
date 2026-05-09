using Build.Harvesting;
using Build.Results;
using Build.Shared.Manifest;

namespace Build.Targets.Harvest.Services;

public interface IBinaryClosureWalker
{
    Task<Result<BinaryClosure, ClosureError>> BuildClosureAsync(LibraryManifest manifest, CancellationToken ct = default);
}
