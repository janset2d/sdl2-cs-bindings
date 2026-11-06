using Build.Data.Manifest.Models;
using Build.Results;
using Build.Targets.Harvest.Models;

namespace Build.Targets.Harvest.Services;
public interface IBinaryClosureWalker
{
    public Task<Result<BinaryClosure, ClosureError>> BuildClosureAsync(LibraryManifest manifest, CancellationToken ct = default);
}
