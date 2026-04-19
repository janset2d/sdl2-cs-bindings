using Build.Context.Models;
using Build.Domain.Harvesting.Results;

namespace Build.Application.Harvesting;

public interface IBinaryClosureWalker
{
    Task<ClosureResult> BuildClosureAsync(LibraryManifest manifest, CancellationToken ct = default);
}
