using Build.Context.Models;
using Build.Modules.Harvesting.Contracts;
using Build.Modules.Harvesting.Models;
using Build.Modules.Vcpkg;
using Cake.Core.IO;

namespace Build.Modules.Harvesting;

public sealed class ArtifactPlanner
{
    private readonly IPackageInfoProvider _pkg;
    private readonly IRuntimeProfile _profile;
    private readonly string _corePackageName;

    public ArtifactPlanner(IPackageInfoProvider pkg, IRuntimeProfile profile, ManifestConfig manifestConfig)
    {
        _pkg = pkg;
        _profile = profile;
        _corePackageName = manifestConfig.LibraryManifests.Single(manifest => manifest.IsCoreLib).VcpkgName;
    }

    public async Task<ArtifactPlan> CreatePlanAsync(LibraryManifest current, BinaryClosure closure, DirectoryPath outRoot, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(closure);
        ArgumentNullException.ThrowIfNull(outRoot);

        var artifacts = new HashSet<NativeArtifact>();
        var copiedPackages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var isCore = current.IsCoreLib;

        foreach (var node in closure.Nodes)
        {
            if (!isCore && (node.OriginPackage.Equals(_corePackageName, StringComparison.OrdinalIgnoreCase)
                           || node.OwnerPackage.Equals(_corePackageName, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var origin = node.Path == closure.PrimaryBinary
                ? ArtifactOrigin.Primary
                : ArtifactOrigin.Runtime;

            artifacts.Add(MakeArtifact(node.Path, node.OwnerPackage, origin, outRoot));
            copiedPackages.Add(node.OwnerPackage);
        }

        foreach (var packageName in copiedPackages)
        {
            var info = await _pkg.GetPackageInfoAsync(packageName, _profile.Triplet, ct);

            if (info is null)
            {
                continue;
            }

            foreach (var lic in info.OwnedFiles.Where(IsLicense))
            {
                artifacts.Add(MakeArtifact(lic, packageName, ArtifactOrigin.License, outRoot));
            }
        }

        return new ArtifactPlan(artifacts);
    }

    private NativeArtifact MakeArtifact(FilePath srcPath, string package, ArtifactOrigin origin, DirectoryPath root)
    {
        var dir = origin == ArtifactOrigin.License
            ? root.Combine(_profile.Rid).Combine("licenses").Combine(package)
            : root.Combine(_profile.Rid).Combine("native");

        var target = dir.CombineWithFilePath(srcPath.GetFilename());

        return new NativeArtifact(srcPath.GetFilename().FullPath, srcPath, target, package, origin);
    }

    private static bool IsLicense(FilePath f) =>
        f.Segments.Contains("share", StringComparer.OrdinalIgnoreCase) &&
        f.GetFilename().FullPath.Equals("copyright", StringComparison.OrdinalIgnoreCase);
}
