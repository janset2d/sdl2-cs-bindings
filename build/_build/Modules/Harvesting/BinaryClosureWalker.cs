#pragma warning disable CA1031, MA0051

using Build.Context.Models;
using Build.Modules.Contracts;
using Build.Modules.Harvesting.Models;
using Build.Modules.Harvesting.Results;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;

namespace Build.Modules.Harvesting;

public sealed class BinaryClosureWalker(IRuntimeScanner runtime, IPackageInfoProvider pkg, IRuntimeProfile profile, ICakeContext ctx) : IBinaryClosureWalker
{
    private readonly IRuntimeScanner _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    private readonly IPackageInfoProvider _pkg = pkg ?? throw new ArgumentNullException(nameof(pkg));
    private readonly IRuntimeProfile _profile = profile ?? throw new ArgumentNullException(nameof(profile));
    private readonly ICakeContext _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
    private readonly ICakeLog _log = ctx.Log;

    public async Task<ClosureResult> BuildClosureAsync(LibraryManifest manifest, CancellationToken ct = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(manifest);

            var rootPkgInfoResult = await _pkg.GetPackageInfoAsync(manifest.VcpkgName, _profile.Triplet, ct);
            if (rootPkgInfoResult.IsError())
            {
                return new ClosureNotFound($"vcpkg info for package {manifest.VcpkgName} not found.");
            }

            var rootPkgInfo = rootPkgInfoResult.PackageInfo;
            var primaryFiles = ResolvePrimaryBinaries(rootPkgInfo, manifest);

            if (primaryFiles.Count == 0)
            {
                return new ClosureError($"No primary binaries found for {manifest.VcpkgName} on {_profile.PlatformFamily}");
            }

            _log.Information("Found {0} primary file(s) for {1}: {2}",
                primaryFiles.Count,
                manifest.VcpkgName,
                string.Join(", ", primaryFiles.Select(f => f.GetFilename().FullPath)));

            var pkgQueue = new Queue<(string OwnerPackage, string OriginPackage)>([(rootPkgInfo.PackageName, rootPkgInfo.PackageName)]);
            var processedPackages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var nodesDict = new Dictionary<FilePath, BinaryNode>();

            while (pkgQueue.TryDequeue(out var package))
            {
                var ownerPackage = package.OwnerPackage;
                var originPackage = package.OriginPackage;

                if (!processedPackages.Add(ownerPackage))
                {
                    continue;
                }

                ct.ThrowIfCancellationRequested();

                var ownerPkgInfoResult = await _pkg.GetPackageInfoAsync(ownerPackage, _profile.Triplet, ct);
                if (ownerPkgInfoResult.IsError())
                {
                    _log.Warning("Package info not found for dependency {0}, continuing.", ownerPackage);
                    continue;
                }

                var ownerPkgInfo = ownerPkgInfoResult.PackageInfo;
                var ownedBinaries = ownerPkgInfo.OwnedFiles.Where(path => IsBinary(path) && !_profile.IsSystemFile(path)).ToList();
                foreach (var bin in ownedBinaries)
                {
                    nodesDict.TryAdd(bin, new BinaryNode(bin, ownerPackage, originPackage));
                }

                originPackage = ownerPkgInfo.PackageName;

                foreach (var depKey in ownerPkgInfo.DeclaredDependencies)
                {
                    if (depKey.StartsWith("vcpkg-", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var idx = depKey.IndexOf(':', StringComparison.OrdinalIgnoreCase);
                    if (idx <= 0)
                    {
                        continue;
                    }

                    pkgQueue.Enqueue((depKey[..idx], originPackage));
                }
            }

            var binQueue = new Queue<FilePath>(nodesDict.Keys);

            while (binQueue.TryDequeue(out var bin))
            {
                ct.ThrowIfCancellationRequested();

                var originPkg = nodesDict[bin].OriginPackage;
                var deps = await _runtime.ScanAsync(bin, ct);

                foreach (var dep in deps)
                {
                    if (_profile.IsSystemFile(dep) || nodesDict.ContainsKey(dep))
                    {
                        continue;
                    }

                    var owner = TryInferPackageNameFromPath(dep) ?? "Unknown";
                    nodesDict[dep] = new BinaryNode(dep, owner, originPkg);
                    binQueue.Enqueue(dep);
                }
            }

            return new BinaryClosure(primaryFiles, [.. nodesDict.Values], processedPackages);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new ClosureError($"Error building dependency closure: {ex.Message}", ex);
        }
    }

    private HashSet<FilePath> ResolvePrimaryBinaries(PackageInfo pkgInfo, LibraryManifest manifest)
    {
        var platformBinaries = manifest.PrimaryBinaries
            .FirstOrDefault(pb => pb.Os.Equals(_profile.PlatformFamily.ToString(), StringComparison.OrdinalIgnoreCase));

        if (platformBinaries == null)
        {
            _log.Warning("No primary binary patterns defined for {0} on {1}", manifest.VcpkgName, _profile.PlatformFamily);
            return new HashSet<FilePath>();
        }

        var primaryFiles = new HashSet<FilePath>();

        foreach (var pattern in platformBinaries.Patterns)
        {
            _log.Debug("Checking pattern '{0}' against {1} owned files", pattern, pkgInfo.OwnedFiles.Count);

            var binaryFiles = pkgInfo.OwnedFiles.Where(f => IsBinary(f)).ToList();
            _log.Debug("Found {0} binary files in package", binaryFiles.Count);

            var matchingFiles = binaryFiles
                .Where(f => MatchesPattern(f.GetFilename().FullPath, pattern) && _ctx.FileExists(f))
                .ToList();

            _log.Debug("Pattern '{0}' matched {1} files", pattern, matchingFiles.Count);

            foreach (var file in matchingFiles)
            {
                primaryFiles.Add(file);
                _log.Information("Pattern '{0}' matched: {1}", pattern, file.GetFilename().FullPath);
            }

            if (matchingFiles.Count == 0)
            {
                _log.Warning("Pattern '{0}' matched no files. Available binary files: {1}",
                    pattern,
                    string.Join(", ", binaryFiles.Select(f => f.GetFilename().FullPath)));
            }
        }

        return primaryFiles;
    }

    private static bool MatchesPattern(string filename, string pattern)
    {
        // Simple glob matching for patterns like "libSDL2.so*" or "libSDL2*.dylib"
        if (!pattern.Contains('*', StringComparison.Ordinal))
        {
            return string.Equals(filename, pattern, StringComparison.OrdinalIgnoreCase);
        }

        // Handle single wildcard patterns
        var parts = pattern.Split('*');
        if (parts.Length == 2)
        {
            var prefix = parts[0];
            var suffix = parts[1];

            // Check prefix match
            if (!string.IsNullOrEmpty(prefix) && !filename.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Check suffix match (only if suffix is not empty)
            if (!string.IsNullOrEmpty(suffix) && !filename.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        // For more complex patterns, we could use a proper glob library
        // For now, this handles our current use cases
        return false;
    }



    private static string? TryInferPackageNameFromPath(FilePath p)
    {
        // .../vcpkg_installed/<triplet>/(bin|lib|share)/<package>/...
        var segments = p.Segments;
        var vcpkgIndex = Array.FindIndex(segments, s => s.Equals("vcpkg_installed", StringComparison.OrdinalIgnoreCase));

        if (vcpkgIndex < 0 || vcpkgIndex + 3 >= segments.Length)
        {
            return null;
        }

        return segments[vcpkgIndex + 3];
    }

    private bool IsBinary(FilePath f)
    {
        var ext = f.GetExtension();
        return _profile.PlatformFamily switch
        {
            PlatformFamily.Windows => string.Equals(ext, ".dll", StringComparison.OrdinalIgnoreCase),
            PlatformFamily.Linux => (string.Equals(ext, ".so", StringComparison.OrdinalIgnoreCase)
                                     || f.GetFilename().FullPath.Contains(".so.", StringComparison.OrdinalIgnoreCase))
                                    && string.Equals(f.GetDirectory().GetDirectoryName(), "lib", StringComparison.Ordinal)
                                    && !string.Equals(f.GetDirectory().GetParent().GetDirectoryName(), "debug", StringComparison.Ordinal),
            PlatformFamily.OSX => string.Equals(ext, ".dylib", StringComparison.OrdinalIgnoreCase),
            _ => false,
        };
    }
}
