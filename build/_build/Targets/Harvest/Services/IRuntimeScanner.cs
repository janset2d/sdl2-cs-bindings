using Cake.Core.IO;

namespace Build.Targets.Harvest.Services;

public interface IRuntimeScanner
{
    Task<IReadOnlySet<FilePath>> ScanAsync(FilePath binary, CancellationToken ct = default);
}
