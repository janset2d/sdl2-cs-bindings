#pragma warning disable S2737, CA1031

using System.Collections.Immutable;
using Build.Tools.Ldd;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;

namespace Build.Targets.Harvest.Services;

public sealed class LinuxLddScanner(ICakeContext context) : IRuntimeScanner
{
    private readonly ICakeContext _context = context ?? throw new ArgumentNullException(nameof(context));
    private readonly ICakeLog _log = context.Log;

    public async Task<IReadOnlySet<FilePath>> ScanAsync(FilePath binary, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(binary);

        try
        {
            var settings = new LddSettings(binary);
            var dependencies = await Task.Run(() => _context.LddDependencies(settings), ct).ConfigureAwait(false);

            var result = new HashSet<FilePath>();

            foreach (var (libName, libPath) in dependencies)
            {
                var filePath = new FilePath(libPath);

                if (_context.FileExists(filePath))
                {
                    result.Add(filePath);
                    _log.Verbose("Added dependency: {0} => {1}", libName, libPath);
                }
                else
                {
                    _log.Verbose("Dependency {0} at {1} not found on filesystem", libName, libPath);
                }
            }

            _log.Verbose("LDD scan of {0} found {1} dependencies", binary.GetFilename(), result.Count);
            return result.ToImmutableHashSet();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _log.Error("LDD scan failed for {0}: {1}", binary.GetFilename(), ex.Message);
            return ImmutableHashSet<FilePath>.Empty;
        }
    }
}

#pragma warning restore S2737, CA1031
