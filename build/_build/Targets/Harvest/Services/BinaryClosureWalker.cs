// File-scope CA1031 + MA0051 suppressions justified by the broad catch in BuildClosureAsync:
// the catch converts every non-cancellation Exception into a typed ClosureBuildError so the
// task surface stays Result<T,TError>-typed. Narrowing to operational types (IOException /
// JsonException / UnauthorizedAccessException / etc., mirroring HarvestTask.IsOperationalHarvestException)
// would tighten the diagnostic surface but changes pre-migration behavior; deferred to a follow-up slice.
#pragma warning disable CA1031, MA0051

using Build.Harvesting;
using Build.DependencyAnalysis;
using Build.Manifest;
using Build.Results;
using Build.Runtime;
using Build.Vcpkg;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;

namespace Build.Targets.Harvest.Services;

public interface IBinaryClosureWalker
{
    Task<Result<BinaryClosure, ClosureError>> BuildClosureAsync(LibraryManifest manifest, CancellationToken ct = default);
}

public sealed class BinaryClosureWalker(IRuntimeScanner runtime, IPackageInfoProvider pkg, IRuntimeProfile profile, ICakeContext ctx) : IBinaryClosureWalker
{
    private readonly IRuntimeScanner _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    private readonly IPackageInfoProvider _pkg = pkg ?? throw new ArgumentNullException(nameof(pkg));
    private readonly IRuntimeProfile _profile = profile ?? throw new ArgumentNullException(nameof(profile));
    private readonly ICakeContext _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
    private readonly ICakeLog _log = ctx.Log;

    public async Task<Result<BinaryClosure, ClosureError>> BuildClosureAsync(LibraryManifest manifest, CancellationToken ct = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(manifest);

            var rootPkgInfoResult = await _pkg.GetPackageInfoAsync(manifest.VcpkgName, _profile.Triplet, ct).ConfigureAwait(false);
            if (rootPkgInfoResult.IsFailure)
            {
                return Result<BinaryClosure, ClosureError>.Failure(new ClosureNotFound($"vcpkg info for package {manifest.VcpkgName} not found."));
            }

            var rootPkgInfo = rootPkgInfoResult.Value;
            var primaryFiles = ResolvePrimaryBinaries(rootPkgInfo, manifest);

            if (primaryFiles.Count == 0)
            {
                return Result<BinaryClosure, ClosureError>.Failure(new ClosureNotFound($"No primary binaries found for {manifest.VcpkgName} on {_profile.Family}"));
            }

            _log.Information("Found {0} primary file(s) for {1}: {2}", primaryFiles.Count, manifest.VcpkgName,
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

                var ownerPkgInfoResult = await _pkg.GetPackageInfoAsync(ownerPackage, _profile.Triplet, ct).ConfigureAwait(false);
                if (ownerPkgInfoResult.IsFailure)
                {
                    _log.Warning("Package info not found for dependency {0}, continuing.", ownerPackage);
                    continue;
                }

                var ownerPkgInfo = ownerPkgInfoResult.Value;
                var ownedBinaries = ownerPkgInfo.OwnedFiles
                    .Select(s => new FilePath(s))
                    .Where(path => IsBinary(path) && !_profile.IsSystemFile(path.GetFilename().FullPath))
                    .ToList();
                foreach (var bin in ownedBinaries)
                {
                    nodesDict.TryAdd(bin, new BinaryNode(bin.FullPath, ownerPackage, originPackage));
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
                var deps = await _runtime.ScanAsync(bin, ct).ConfigureAwait(false);

                foreach (var dep in deps)
                {
                    if (_profile.IsSystemFile(dep.GetFilename().FullPath) || nodesDict.ContainsKey(dep))
                    {
                        continue;
                    }

                    var owner = TryInferPackageNameFromPath(dep) ?? "Unknown";
                    nodesDict[dep] = new BinaryNode(dep.FullPath, owner, originPkg);
                    binQueue.Enqueue(dep);
                }
            }

            var primaryFilesAsStrings = primaryFiles.Select(f => f.FullPath).ToHashSet(StringComparer.Ordinal);
            return Result<BinaryClosure, ClosureError>.Success(new BinaryClosure(primaryFilesAsStrings, [.. nodesDict.Values], processedPackages));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Result<BinaryClosure, ClosureError>.Failure(new ClosureBuildError($"Error building dependency closure: {ex.Message}", ex));
        }
    }

    private HashSet<FilePath> ResolvePrimaryBinaries(PackageInfo pkgInfo, LibraryManifest manifest)
    {
        var platformBinaries = manifest.PrimaryBinaries
            .FirstOrDefault(pb => pb.Os.Equals(_profile.Family.ToString(), StringComparison.OrdinalIgnoreCase));

        if (platformBinaries == null)
        {
            _log.Warning("No primary binary patterns defined for {0} on {1}", manifest.VcpkgName, _profile.Family);
            return [];
        }

        var primaryFiles = new HashSet<FilePath>();

        foreach (var pattern in platformBinaries.Patterns)
        {
            _log.Debug("Checking pattern '{0}' against {1} owned files", pattern, pkgInfo.OwnedFiles.Count);

            var binaryFiles = pkgInfo.OwnedFiles.Select(s => new FilePath(s)).Where(IsBinary).ToList();
            _log.Debug("Found {0} binary files in package", binaryFiles.Count);

            var matchingFiles = binaryFiles
                .Where(f => MatchesPattern(f.GetFilename().FullPath, pattern) && _ctx.FileExists(f))
                .ToList();

            _log.Debug("Pattern '{0}' matched {1} files", pattern, matchingFiles.Count);

            foreach (var file in matchingFiles)
            {
                primaryFiles.Add(file);
                _log.Debug("Pattern '{0}' matched: {1}", pattern, file.GetFilename().FullPath);
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

    internal static bool MatchesPattern(string filename, string pattern)
    {
        if (!pattern.Contains('*', StringComparison.Ordinal))
        {
            return string.Equals(filename, pattern, StringComparison.OrdinalIgnoreCase);
        }

        var parts = pattern.Split('*');
        if (parts.Length != 2)
        {
            return false;
        }

        var prefix = parts[0];
        var suffix = parts[1];

        if (!string.IsNullOrEmpty(prefix) && !filename.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.IsNullOrEmpty(suffix) || filename.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
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
        return _profile.Family switch
        {
            RuntimeFamily.Windows => string.Equals(ext, ".dll", StringComparison.OrdinalIgnoreCase)
                                      && string.Equals(f.GetDirectory().GetDirectoryName(), "bin", StringComparison.OrdinalIgnoreCase),
            RuntimeFamily.Linux => (string.Equals(ext, ".so", StringComparison.OrdinalIgnoreCase)
                                     || f.GetFilename().FullPath.Contains(".so.", StringComparison.Ordinal))
                                    && string.Equals(f.GetDirectory().GetDirectoryName(), "lib", StringComparison.Ordinal)
                                    && !string.Equals(f.GetDirectory().GetParent().GetDirectoryName(), "debug", StringComparison.Ordinal),
            RuntimeFamily.OSX => string.Equals(ext, ".dylib", StringComparison.Ordinal)
                                  && string.Equals(f.GetDirectory().GetDirectoryName(), "lib", StringComparison.Ordinal)
                                  && !string.Equals(f.GetDirectory().GetParent().GetDirectoryName(), "debug", StringComparison.Ordinal),
            _ => false,
        };
    }
}
