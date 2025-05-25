#pragma warning disable S2737

using System.Collections.Immutable;
using Build.Modules.Contracts;
using Build.Tools.Ldd;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;

namespace Build.Modules.DependencyAnalysis;

public sealed class LinuxLddScanner : IRuntimeScanner
{
    private readonly ICakeContext _context;
    private readonly ICakeLog _log;

    public LinuxLddScanner(ICakeContext context, ICakeLog log)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

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
                // Skip virtual DSOs and system paths
                if (IsVirtualOrSystemLibrary(libName, libPath))
                {
                    _log.Verbose("Skipping system/virtual library: {0} => {1}", libName, libPath);
                    continue;
                }
                
                var filePath = new FilePath(libPath);
                if (_context.FileExists(filePath))
                {
                    result.Add(filePath);
                    _log.Verbose("Added dependency: {0} => {1}", libName, libPath);
                }
                else
                {
                    _log.Warning("Dependency {0} at {1} not found on filesystem", libName, libPath);
                }
            }

            _log.Information("LDD scan of {0} found {1} dependencies", binary.GetFilename(), result.Count);
            return result.ToImmutableHashSet();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (InvalidOperationException ex)
        {
            _log.Error("LDD scan failed for {0}: {1}", binary.GetFilename(), ex.Message);
            return ImmutableHashSet<FilePath>.Empty;
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            _log.Error("LDD tool not found or failed for {0}: {1}", binary.GetFilename(), ex.Message);
            return ImmutableHashSet<FilePath>.Empty;
        }
        catch (FileNotFoundException ex)
        {
            _log.Error("Binary file not found for LDD scan {0}: {1}", binary.GetFilename(), ex.Message);
            return ImmutableHashSet<FilePath>.Empty;
        }
    }

    private static bool IsVirtualOrSystemLibrary(string libName, string libPath)
    {
        // Skip virtual DSOs
        if (libName.Equals("linux-vdso.so.1", StringComparison.OrdinalIgnoreCase))
            return true;
        
        // Skip system paths - libraries in standard system directories
        return libPath.StartsWith("/lib/", StringComparison.OrdinalIgnoreCase) ||
               libPath.StartsWith("/usr/lib/", StringComparison.OrdinalIgnoreCase) ||
               libPath.StartsWith("/lib64/", StringComparison.OrdinalIgnoreCase) ||
               libPath.StartsWith("/usr/lib64/", StringComparison.OrdinalIgnoreCase);
    }
}
