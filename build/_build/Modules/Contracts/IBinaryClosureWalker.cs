using Build.Context.Models;
using Build.Modules.Harvesting.Results;

namespace Build.Modules.Contracts;

public interface IBinaryClosureWalker
{
    Task<ClosureResult> BuildClosureAsync(LibraryManifest manifest, CancellationToken ct = default);
}
