#pragma warning disable S2737, CA1031

using System.Collections.Immutable;
using Build.Modules.Contracts;
using Build.Tools.Otool;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.IO;
using Cake.Core.Diagnostics;

namespace Build.Modules.DependencyAnalysis;

public class MacOtoolScanner : IRuntimeScanner
{
    private readonly ICakeContext _context;
    private readonly ICakeLog _log;

    public MacOtoolScanner(ICakeContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _log = context.Log;
    }

    public async Task<IReadOnlySet<FilePath>> ScanAsync(FilePath binary, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(binary);

        try
        {
            var settings = new OtoolSettings(binary);
            var dependencies = await Task.Run(() => _context.OtoolDependencies(settings), ct).ConfigureAwait(false);

            var result = new HashSet<FilePath>();

            foreach (var (libName, libPath) in dependencies)
            {
                // For @rpath/@loader_path references, we need to try to resolve them
                var resolvedPath = ResolveLibraryPath(libPath, binary);

                if (resolvedPath != null && _context.FileExists(resolvedPath))
                {
                    result.Add(resolvedPath);
                    _log.Verbose("Added dependency: {0} => {1}", libName, resolvedPath);
                }
                else if (!libPath.StartsWith("/System/", StringComparison.Ordinal) && !libPath.StartsWith("/usr/lib/", StringComparison.Ordinal))
                {
                    // Log missing user libraries (but not system ones)
                    _log.Verbose("Dependency {0} at {1} not found or unresolved", libName, libPath);
                }
            }

            _log.Verbose("Otool scan of {0} found {1} dependencies", binary.GetFilename(), result.Count);
            return result.ToImmutableHashSet();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _log.Error("Otool scan failed for {0}: {1}", binary.GetFilename(), ex.Message);
            return ImmutableHashSet<FilePath>.Empty;
        }
    }

    private FilePath? ResolveLibraryPath(string libPath, FilePath binary)
    {
        // Handle different path types:
        // Absolute paths: return as-is if they exist
        if (System.IO.Path.IsPathRooted(libPath) && !libPath.StartsWith('@'))
        {
            return new FilePath(libPath);
        }

        // @rpath: Try to resolve relative to binary's directory
        if (libPath.StartsWith("@rpath/", StringComparison.Ordinal))
        {
            var relativePath = libPath.Substring("@rpath/".Length);
            var binaryDir = binary.GetDirectory();
            var resolvedPath = binaryDir.CombineWithFilePath(relativePath);

            if (_context.FileExists(resolvedPath))
            {
                return resolvedPath;
            }

            // Try lib subdirectory
            var libSubDir = binaryDir.Combine("../lib").CombineWithFilePath(relativePath);
            if (_context.FileExists(libSubDir))
            {
                return libSubDir;
            }
        }

        // @loader_path: Resolve relative to the binary's directory
        if (libPath.StartsWith("@loader_path/", StringComparison.Ordinal))
        {
            var relativePath = libPath.Substring("@loader_path/".Length);
            var binaryDir = binary.GetDirectory();
            var resolvedPath = binaryDir.CombineWithFilePath(relativePath);

            if (_context.FileExists(resolvedPath))
            {
                return resolvedPath;
            }
        }

        // @executable_path: For now, treat similar to @loader_path
        if (libPath.StartsWith("@executable_path/", StringComparison.Ordinal))
        {
            var relativePath = libPath.Substring("@executable_path/".Length);
            var binaryDir = binary.GetDirectory();
            var resolvedPath = binaryDir.CombineWithFilePath(relativePath);

            if (_context.FileExists(resolvedPath))
            {
                return resolvedPath;
            }
        }

        return null;
    }
}
