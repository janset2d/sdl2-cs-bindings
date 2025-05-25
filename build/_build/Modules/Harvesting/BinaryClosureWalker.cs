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
            var libName = manifest.LibNames.First(x => x.Os.Equals(_profile.OsFamily, StringComparison.OrdinalIgnoreCase)).Name;
            var primary = await ResolvePrimaryBinaryAsync(rootPkgInfo, libName);

            if (primary is null || !_ctx.FileExists(primary))
            {
                return new ClosureError($"Primary binary '{libName}' not found for {manifest.VcpkgName}");
            }

            var pkgQueue = new Queue<(string OwnerPackage, string OriginPackage)>([(rootPkgInfo.PackageName, rootPkgInfo.PackageName)]);
            var seenPkgs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var nodesDict = new Dictionary<FilePath, BinaryNode>();

            while (pkgQueue.TryDequeue(out var package))
            {
                var ownerPackage = package.OwnerPackage;
                var originPackage = package.OriginPackage;

                if (!seenPkgs.Add(ownerPackage))
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

            return new BinaryClosure(primary, [.. nodesDict.Values], seenPkgs);
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
    private async Task<FilePath?> ResolvePrimaryBinaryAsync(PackageInfo pkgInfo, string expectedName)
    {
        // Try direct match first (Windows style or exact match)
        var directMatch = pkgInfo.OwnedFiles.FirstOrDefault(f => 
            f.GetFilename().FullPath.Equals(expectedName, StringComparison.OrdinalIgnoreCase));
        
        if (directMatch != null && _ctx.FileExists(directMatch))
        {
            _log.Verbose("Found primary binary via direct match: {0}", directMatch.FullPath);
            return directMatch;
        }
        
        // For Unix: Find the real file in the symlink chain
        if (_profile.OsFamily != "Windows")
        {
            var unixPrimary = await ResolveUnixPrimaryBinaryAsync(pkgInfo, expectedName);
            if (unixPrimary != null)
            {
                _log.Verbose("Found primary binary via Unix symlink resolution: {0}", unixPrimary.FullPath);
                return unixPrimary;
            }
        }
        
        _log.Warning("Could not resolve primary binary '{0}' for package", expectedName);
        return null;
    }

    private async Task<FilePath?> ResolveUnixPrimaryBinaryAsync(PackageInfo pkgInfo, string expectedName)
    {
        // Find all potential symlink chain members
        // For libSDL2.so, look for libSDL2.so, libSDL2-2.0.so, libSDL2-2.0.so.0, etc.
        var baseNameWithoutExt = expectedName.Replace(".so", "", StringComparison.OrdinalIgnoreCase);
        var candidates = pkgInfo.OwnedFiles
            .Where(f => IsBinary(f) && 
                       f.GetFilename().FullPath.StartsWith(baseNameWithoutExt, StringComparison.OrdinalIgnoreCase))
            .ToList();
        
        _log.Verbose("Found {0} potential symlink candidates for {1}", candidates.Count, expectedName);
        
        // Find the real file (not a symlink) - this is typically the most versioned one
        var existingCandidates = candidates
            .Where(c => _ctx.FileExists(c))
            .OrderByDescending(f => f.GetFilename().FullPath.Length);
            
        foreach (var candidate in existingCandidates)
        {
            var isSymlink = await IsSymlinkAsync(candidate);
            if (!isSymlink)
            {
                _log.Verbose("Found real file in symlink chain: {0}", candidate.FullPath);
                return candidate;
            }
        }
        
        // Fallback: return the first existing candidate
        var fallback = candidates.FirstOrDefault(c => _ctx.FileExists(c));
        if (fallback != null)
        {
            _log.Verbose("Using fallback candidate: {0}", fallback.FullPath);
        }
        
        return fallback;
    }

    private static async Task<bool> IsSymlinkAsync(FilePath path)
    {
        try
        {
            // Use FileInfo to detect symlinks
            var fileInfo = await Task.Run(() => new FileInfo(path.FullPath));
            return fileInfo.Exists && fileInfo.LinkTarget != null;
        }
        catch (UnauthorizedAccessException)
        {
            // If we can't access the file, assume it's not a symlink
            return false;
        }
        catch (IOException)
        {
            // If there's an IO error, assume it's not a symlink
            return false;
        }
    }

    private string? TryInferPackageNameFromPath(FilePath p)
    {
        // .../vcpkg_installed/<triplet>/(bin|lib|share)/<package>/...
        var segments = p.Segments;
        var vcpkgIndex = Array.FindIndex(segments, s => s.Equals("vcpkg_installed", StringComparison.OrdinalIgnoreCase));
        
        if (vcpkgIndex < 0 || vcpkgIndex + 2 >= segments.Length)
            return null;
        
        var subdirIndex = vcpkgIndex + 2; // bin, lib, or share
        var subdir = segments[subdirIndex];
        
        // For lib directory on Unix, libraries are directly in lib/, infer from filename
        if (subdir.Equals("lib", StringComparison.OrdinalIgnoreCase) && _profile.OsFamily != "Windows")
        {
            return InferPackageFromLibraryName(p.GetFilename().FullPath);
        }
        
        // For bin/share, or Windows lib, use directory structure
        return vcpkgIndex + 3 < segments.Length ? segments[vcpkgIndex + 3] : null;
    }

    private static string? InferPackageFromLibraryName(string filename)
    {
        // libSDL2-2.0.so.0.3200.4 -> sdl2
        // libwebp.so.7.1.10 -> libwebp
        // libSDL2_image-2.0.so.0.800.8 -> sdl2-image
        
        if (!filename.StartsWith("lib", StringComparison.OrdinalIgnoreCase))
            return null;
        
        var nameWithoutLib = filename[3..]; // Remove "lib" prefix
        var nameWithoutExt = nameWithoutLib.Split('.')[0]; // Remove version/extension
        
        // Handle special cases for SDL libraries
        return nameWithoutExt.ToLowerInvariant() switch
        {
            "sdl2" or "sdl2-2" => "sdl2",
            "sdl2_image" or "sdl2_image-2" => "sdl2-image",
            "sdl2_mixer" or "sdl2_mixer-2" => "sdl2-mixer", 
            "sdl2_ttf" or "sdl2_ttf-2" => "sdl2-ttf",
            "sdl2_gfx" or "sdl2_gfx-1" => "sdl2-gfx",
            _ => nameWithoutExt.ToLowerInvariant()
        };
    }

    private bool IsBinary(FilePath f)
    {
        var ext = f.GetExtension();
        return _profile.OsFamily switch
        {
            "Windows" => string.Equals(ext, ".dll", StringComparison.OrdinalIgnoreCase),
            "Linux" => string.Equals(ext, ".so", StringComparison.OrdinalIgnoreCase) || 
                      f.GetFilename().FullPath.Contains(".so.", StringComparison.OrdinalIgnoreCase),
            "OSX" => string.Equals(ext, ".dylib", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }
}
