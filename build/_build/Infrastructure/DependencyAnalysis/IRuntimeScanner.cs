using Cake.Core.IO;

namespace Build.Infrastructure.DependencyAnalysis;

public interface IRuntimeScanner
{
    Task<IReadOnlySet<FilePath>> ScanAsync(FilePath binary, CancellationToken ct = default);
}
