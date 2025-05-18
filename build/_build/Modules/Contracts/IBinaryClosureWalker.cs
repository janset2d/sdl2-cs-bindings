using Build.Context.Models;
using Build.Modules.Harvesting.Models;

namespace Build.Modules.Contracts;

public interface IBinaryClosureWalker
{
    Task<BinaryClosure?> BuildClosureAsync(LibraryManifest manifest, CancellationToken ct = default);
}
