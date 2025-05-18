using Cake.Core.IO;

namespace Build.Modules.Contracts;

public interface IRuntimeScanner
{
    Task<IReadOnlySet<FilePath>> ScanAsync(FilePath binary, CancellationToken ct = default);
}
