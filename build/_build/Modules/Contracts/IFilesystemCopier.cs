using Build.Modules.Harvesting.Models;

namespace Build.Modules.Contracts;

public interface IFilesystemCopier
{
    Task CopyAsync(IEnumerable<NativeArtifact> artifacts, CancellationToken ct = default);
}
