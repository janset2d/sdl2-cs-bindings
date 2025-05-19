#pragma warning disable CA1031

using Build.Context.Models;
using Build.Modules.Contracts;
using Build.Modules.Harvesting.Models;
using Build.Modules.Harvesting.Results;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;

namespace Build.Modules.Harvesting;

public sealed class ArtifactPlanner : IArtifactPlanner
{
    private readonly IPackageInfoProvider _pkg;
    private readonly IRuntimeProfile _profile;
    private readonly ICakeLog _log;
    private readonly string _corePackageName;

    public ArtifactPlanner(IPackageInfoProvider pkg, IRuntimeProfile profile, ICakeContext context, ManifestConfig manifestConfig)
    {
        ArgumentNullException.ThrowIfNull(manifestConfig);

        _pkg = pkg ?? throw new ArgumentNullException(nameof(pkg));
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));
        _log = context?.Log ?? throw new ArgumentNullException(nameof(context));
        _corePackageName = manifestConfig.LibraryManifests.Single(manifest => manifest.IsCoreLib).VcpkgName;
    }

    public async Task<ArtifactPlannerResult> CreatePlanAsync(LibraryManifest current, BinaryClosure closure, DirectoryPath outRoot, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(closure);
        ArgumentNullException.ThrowIfNull(outRoot);

        try
        {
            var artifacts = new HashSet<NativeArtifact>();
            var copiedPackages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var isCore = current.IsCoreLib;
            var currentPackageName = current.Name;

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

                var nativeArtifact = MakeArtifact(node.Path, currentPackageName, node.OwnerPackage, origin, outRoot);
                artifacts.Add(nativeArtifact);
                copiedPackages.Add(node.OwnerPackage);
            }

            foreach (var packageName in copiedPackages)
            {
                var info = await _pkg.GetPackageInfoAsync(packageName, _profile.Triplet, ct);

                if (info.IsError())
                {
                    _log.Warning("Package info not found for dependency {0}, continuing.", packageName);
                    continue;
                }

                foreach (var licensePath in info.PackageInfo.OwnedFiles.Where(IsLicense))
                {
                    var licenseArtifact = MakeArtifact(licensePath, currentPackageName, packageName, ArtifactOrigin.License, outRoot);
                    artifacts.Add(licenseArtifact);
                }
            }

            return new ArtifactPlan(artifacts);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new ArtifactPlannerError($"Error while planning artifacts: {ex.Message}", ex);
        }
    }

    private NativeArtifact MakeArtifact(FilePath srcPath, string currentPackageName, string ownerPackageName, ArtifactOrigin origin, DirectoryPath root)
    {
        var dir = origin == ArtifactOrigin.License
            ? root.Combine(currentPackageName).Combine("licenses").Combine(ownerPackageName)
            : root.Combine(currentPackageName).Combine("runtimes").Combine(_profile.Rid).Combine("native");

        var target = dir.CombineWithFilePath(srcPath.GetFilename());

        return new NativeArtifact(srcPath.GetFilename().FullPath, srcPath, target, ownerPackageName, origin);
    }

    private static bool IsLicense(FilePath f) =>
        f.Segments.Contains("share", StringComparer.OrdinalIgnoreCase) &&
        f.GetFilename().FullPath.Equals("copyright", StringComparison.OrdinalIgnoreCase);
}
