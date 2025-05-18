using System.Collections.Immutable;
using Build.Modules.Contracts;
using Cake.Core.IO;
using Cake.Core.Diagnostics;

namespace Build.Modules.DependencyAnalysis;

public class LinuxLddScanner : IRuntimeScanner
{
    private readonly ICakeLog _log;

    public LinuxLddScanner(ICakeLog log)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public Task<IReadOnlySet<FilePath>> ScanAsync(FilePath binary, CancellationToken ct = default)
    {
        _log.Warning("LinuxLddScanner.ScanAsync is not fully implemented yet. Returning empty dependencies for {0}.", binary.GetFilename());

        return Task.FromResult<IReadOnlySet<FilePath>>(ImmutableHashSet<FilePath>.Empty);
    }
}
