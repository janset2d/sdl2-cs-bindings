using Build.Modules.Harvesting.Models;
using Build.Modules.Harvesting.Results;

namespace Build.Modules.Contracts;

public interface IFilesystemCopier
{
    Task<CopierResult> CopyAsync(IEnumerable<NativeArtifact> artifacts, CancellationToken ct = default);
}
