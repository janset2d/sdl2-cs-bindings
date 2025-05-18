using Build.Context.Models;
using Build.Modules.Contracts;
using Build.Modules.Harvesting.Models;
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

#pragma warning disable MA0051
    public async Task<BinaryClosure?> BuildClosureAsync(LibraryManifest manifest, CancellationToken ct = default)
#pragma warning restore MA0051
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var rootInfo = await _pkg.GetPackageInfoAsync(manifest.VcpkgName, _profile.Triplet, ct);
        if (rootInfo is null)
        {
            _log.Error("vcpkg info for package {0} not found.", manifest.VcpkgName);
            return null;
        }

        var libName = manifest.LibNames.First(x => x.Os.Equals(_profile.OsFamily, StringComparison.OrdinalIgnoreCase)).Name;
        var primary = rootInfo.OwnedFiles.FirstOrDefault(f => f.GetFilename().FullPath.Equals(libName, StringComparison.OrdinalIgnoreCase));

        if (primary is null || !_ctx.FileExists(primary))
        {
            throw new CakeException($"Primary binary '{libName}' not found for {manifest.VcpkgName}");
        }

        var pkgQueue = new Queue<(string OwnerPackage, string OriginPackage)>([(rootInfo.PackageName, rootInfo.PackageName)]);
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

            var info = await _pkg.GetPackageInfoAsync(ownerPackage, _profile.Triplet, ct);
            if (info is null)
            {
                continue;
            }

            var ownedBinaries = info.OwnedFiles.Where(IsBinary).ToList();
            foreach (var bin in ownedBinaries)
            {
                nodesDict.TryAdd(bin, new BinaryNode(bin, ownerPackage, originPackage));
            }

            originPackage = info.PackageName;

            foreach (var depKey in info.DeclaredDependencies)
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

    // helper -------------------------------------------------------------------
    private static string? TryInferPackageNameFromPath(FilePath p)
    {
        // .../vcpkg_installed/<triplet>/(bin|lib|share)/<package>/...
        var seg = p.Segments;
        var idx = Array.FindIndex(seg, s => s.Equals("vcpkg_installed", StringComparison.OrdinalIgnoreCase));
        return idx < 0 || idx + 3 >= seg.Length ? null : seg[idx + 3];
    }

    private static bool IsBinary(FilePath f)
    {
        return string.Equals(f.GetExtension(), ".dll", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(f.GetExtension(), ".so", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(f.GetExtension(), ".dylib", StringComparison.OrdinalIgnoreCase);
    }
}
