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
            var libName = manifest.LibNames.First(x => x.Os.Equals(_profile.PlatformFamily.ToString(), StringComparison.OrdinalIgnoreCase)).Name;
            var primary = ResolvePrimaryBinaryAsync(rootPkgInfo, libName);

            if (primary is null || !_ctx.FileExists(primary))
            {
                return new ClosureError($"Primary binary '{libName}' not found for {manifest.VcpkgName}");
            }

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
                var ownedBinaries = ownerPkgInfo.OwnedFiles.Where(IsBinary).ToList();
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

            return new BinaryClosure(primary, [.. nodesDict.Values], processedPackages);
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

    // helper -------------------------------------------------------------------
    private FilePath? ResolvePrimaryBinaryAsync(PackageInfo pkgInfo, string expectedName)
    {
        // Try a direct match first (Windows style or exact match)
        var directMatch = pkgInfo.OwnedFiles.FirstOrDefault(f => f.GetFilename().FullPath.Equals(expectedName, StringComparison.OrdinalIgnoreCase));

        if (directMatch != null && _ctx.FileExists(directMatch))
        {
            _log.Verbose("Found primary binary via direct match: {0}", directMatch.FullPath);
            return directMatch;
        }

        // For Unix: Find the real file in the symlink chain
        if (_profile.PlatformFamily != PlatformFamily.Windows)
        {
            var unixPrimary = ResolveUnixPrimaryBinary(pkgInfo, expectedName);
            if (unixPrimary != null)
            {
                _log.Verbose("Found primary binary via Unix symlink resolution: {0}", unixPrimary.FullPath);
                return unixPrimary;
            }
        }

        _log.Warning("Could not resolve primary binary '{0}' for package", expectedName);
        return null;
    }

    private FilePath? ResolveUnixPrimaryBinary(PackageInfo pkgInfo, string expectedName)
    {
        // Find all potential symlink chain members
        // For libSDL2.so, look for libSDL2.so, libSDL2-2.0.so, libSDL2-2.0.so.0, etc.
        var baseNameWithoutExt = expectedName.Replace(".so", "", StringComparison.OrdinalIgnoreCase);
        var candidates = pkgInfo.OwnedFiles
            .Where(f => IsBinary(f) && f.GetFilename().FullPath.StartsWith(baseNameWithoutExt, StringComparison.OrdinalIgnoreCase))
            .ToList();

        _log.Verbose("Found {0} potential symlink candidates for {1}", candidates.Count, expectedName);

        // Find the real file (not a symlink) - this is typically the most versioned one
        var existingCandidates = candidates
            .Where(c => _ctx.FileExists(c))
            .OrderByDescending(f => f.GetFilename().FullPath.Length);

        foreach (var candidate in existingCandidates)
        {
            var isSymlink = IsSymlink(candidate);
            if (isSymlink)
            {
                continue;
            }

            _log.Verbose("Found real file in symlink chain: {0}", candidate.FullPath);
            return candidate;
        }

        // Fallback: return the first existing candidate
        var fallback = candidates.FirstOrDefault(c => _ctx.FileExists(c));
        if (fallback != null)
        {
            _log.Verbose("Using fallback candidate: {0}", fallback.FullPath);
        }

        return fallback;
    }

    private static bool IsSymlink(FilePath path)
    {
        try
        {
            var fileInfo = new FileInfo(path.FullPath);
            return fileInfo.Exists && fileInfo.LinkTarget != null;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
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

        // Use directory structure when available (Windows bin/, or Unix with subdirectories)
        return segments[vcpkgIndex + 3];
    }

    private bool IsBinary(FilePath f)
    {
        var ext = f.GetExtension();
        return _profile.PlatformFamily switch
        {
            PlatformFamily.Windows => string.Equals(ext, ".dll", StringComparison.OrdinalIgnoreCase),
            PlatformFamily.Linux => string.Equals(ext, ".so", StringComparison.OrdinalIgnoreCase) || f.GetFilename().FullPath.Contains(".so.", StringComparison.OrdinalIgnoreCase),
            PlatformFamily.OSX => string.Equals(ext, ".dylib", StringComparison.OrdinalIgnoreCase),
            _ => false,
        };
    }
}
