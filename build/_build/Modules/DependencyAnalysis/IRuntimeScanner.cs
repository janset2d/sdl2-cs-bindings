namespace Build.Modules.DependencyAnalysis;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cake.Core.IO;

public interface IRuntimeScanner
{
    // Returns full paths to dependent binaries
    Task<IReadOnlySet<FilePath>> ScanAsync(FilePath binary, CancellationToken ct = default);
}
