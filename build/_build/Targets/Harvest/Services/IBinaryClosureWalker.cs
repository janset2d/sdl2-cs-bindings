using Build.Harvesting;
using Build.Manifest;
using Build.Results;

namespace Build.Targets.Harvest.Services;

public interface IBinaryClosureWalker
{
    Task<Result<BinaryClosure, ClosureError>> BuildClosureAsync(LibraryManifest manifest, CancellationToken ct = default);
}
