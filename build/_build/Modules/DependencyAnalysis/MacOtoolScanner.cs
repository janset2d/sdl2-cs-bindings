namespace Build.Modules.DependencyAnalysis;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Cake.Core.IO;
using Cake.Core.Diagnostics; // For ICakeLog if needed
using Cake.Core; // For ICakeContext if needed, or for logging extensions

public class MacOtoolScanner : IRuntimeScanner
{
    private readonly ICakeLog _log;

    public MacOtoolScanner(ICakeLog log)
    {
        _log = log ?? throw new System.ArgumentNullException(nameof(log));
    }

    public Task<IReadOnlySet<FilePath>> ScanAsync(FilePath binary, CancellationToken ct = default)
    {
        _log.Warning("MacOtoolScanner.ScanAsync is not fully implemented yet. Returning empty dependencies for {0}.", binary.GetFilename());
        // TODO: Implement otool parsing logic
        return Task.FromResult<IReadOnlySet<FilePath>>(ImmutableHashSet<FilePath>.Empty);
    }
}
