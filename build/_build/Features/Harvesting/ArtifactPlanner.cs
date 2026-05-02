#pragma warning disable CA1031

using System.Diagnostics.CodeAnalysis;
using Build.Host.Paths;
using Build.Integrations.Vcpkg;
using Build.Shared.Harvesting;
using Build.Shared.Manifest;
using Build.Shared.Runtime;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;

namespace Build.Features.Harvesting;

public sealed class ArtifactPlanner : IArtifactPlanner
{
    private readonly IPackageInfoProvider _pkg;
    private readonly IRuntimeProfile _profile;
    private readonly IPathService _pathService;
    private readonly ICakeLog _log;
    private readonly string _corePackageName;
    private readonly ICakeEnvironment _environment;

    public ArtifactPlanner(IPackageInfoProvider pkg, IRuntimeProfile profile, IPathService pathService, ICakeContext context, ManifestConfig manifestConfig)
    {
        ArgumentNullException.ThrowIfNull(manifestConfig);
        ArgumentNullException.ThrowIfNull(context);

        _pkg = pkg ?? throw new ArgumentNullException(nameof(pkg));
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));
        _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));
        _environment = context.Environment;
        _log = context.Log;
        _corePackageName = manifestConfig.CoreLibrary.VcpkgName;
    }

    [SuppressMessage("Design", "MA0051:Method is too long")]
    public async Task<ArtifactPlannerResult> CreatePlanAsync(LibraryManifest current, BinaryClosure closure, DirectoryPath outRoot, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(closure);
        ArgumentNullException.ThrowIfNull(outRoot);

        try
        {
            var actions = new List<DeploymentAction>();
            var copiedPackages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var itemsForUnixArchive = new List<ArchivedItemDetails>();

            var isCore = current.IsCoreLib;
            var currentLibraryName = current.Name;

            var nativeOutput = outRoot.Combine(currentLibraryName).Combine("runtimes").Combine(_profile.Rid).Combine("native");
            // Post-H1 (2026-04-18): licenses are written RID-scoped under licenses/{rid}/{package}/...
            // so sequential multi-RID harvests preserve each RID's license attribution instead of
            // overwriting library-flat. ConsolidateHarvestTask unions all successful RIDs into
            // licenses/_consolidated/ which is what PackageTask consumes at pack time.
            var licenseOutput = outRoot.Combine(currentLibraryName).Combine("licenses").Combine(_profile.Rid);

            foreach (var node in closure.Nodes)
            {
                ct.ThrowIfCancellationRequested();

                var ownerPackageName = node.OwnerPackage;
                var originPackage = node.OriginPackage;

                if (!isCore && (originPackage.Equals(_corePackageName, StringComparison.OrdinalIgnoreCase)
                                || ownerPackageName.Equals(_corePackageName, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var filePath = new FilePath(node.Path);
                var origin = closure.IsPrimaryFile(node.Path) ? ArtifactOrigin.Primary : ArtifactOrigin.Runtime;

                if (_environment.Platform.Family == PlatformFamily.Windows)
                {
                    var targetPath = nativeOutput.CombineWithFilePath(filePath.GetFilename().FullPath);
                    actions.Add(new FileCopyAction(filePath, targetPath, ownerPackageName, origin));
                }
                else
                {
                    itemsForUnixArchive.Add(new ArchivedItemDetails(filePath, ownerPackageName, origin));
                }

                copiedPackages.Add(ownerPackageName);
            }

            foreach (var packageName in copiedPackages)
            {
                ct.ThrowIfCancellationRequested();
                var infoResult = await _pkg.GetPackageInfoAsync(packageName, _profile.Triplet, ct);

                if (infoResult.IsError())
                {
                    _log.Warning("Package info not found for dependency {0}, continuing.", packageName);
                    continue;
                }

                foreach (var licensePathString in infoResult.PackageInfo.OwnedFiles)
                {
                    var licensePath = new FilePath(licensePathString);
                    if (!IsLicense(licensePath))
                    {
                        continue;
                    }

                    var licenseTargetPath = licenseOutput.Combine(packageName).CombineWithFilePath(licensePath.GetFilename().FullPath);
                    actions.Add(new FileCopyAction(licensePath, licenseTargetPath, packageName, ArtifactOrigin.License));
                }
            }

            if (_environment.Platform.Family != PlatformFamily.Windows && itemsForUnixArchive.Count != 0)
            {
                const string archiveName = "native.tar.gz";
                _log.Verbose("Archive target directory for {0}: {1}", currentLibraryName, nativeOutput.FullPath);

                var archiveFinalPath = nativeOutput.CombineWithFilePath(archiveName);
                var vcpkgInstalledLibDir = _pathService.GetVcpkgInstalledLibDir(_profile.Triplet);

                _log.Debug("Base directory for tar archive: {0}", nativeOutput.FullPath);
                actions.Add(new ArchiveCreationAction(archiveFinalPath, vcpkgInstalledLibDir, itemsForUnixArchive, archiveName));
            }

            // Create deployment statistics
            var primaryFiles = new List<FileDeploymentInfo>();
            var runtimeFiles = new List<FileDeploymentInfo>();
            var licenseFiles = new List<FileDeploymentInfo>();
            var deployedPackages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var filteredPackages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Analyze what's being deployed
            foreach (var action in actions)
            {
                switch (action)
                {
                    case FileCopyAction fileCopy:
                        switch (fileCopy.Origin)
                        {
                            case ArtifactOrigin.Primary:
                                primaryFiles.Add(new FileDeploymentInfo(fileCopy.SourcePath, fileCopy.PackageName, DeploymentLocation.FileSystem));
                                break;
                            case ArtifactOrigin.Runtime:
                                runtimeFiles.Add(new FileDeploymentInfo(fileCopy.SourcePath, fileCopy.PackageName, DeploymentLocation.FileSystem));
                                break;
                            case ArtifactOrigin.License:
                                licenseFiles.Add(new FileDeploymentInfo(fileCopy.SourcePath, fileCopy.PackageName, DeploymentLocation.FileSystem));
                                break;
                        }

                        deployedPackages.Add(fileCopy.PackageName);
                        break;
                    case ArchiveCreationAction archiveAction:
                        foreach (var item in archiveAction.ItemsToArchive)
                        {
                            switch (item.Origin)
                            {
                                case ArtifactOrigin.Primary:
                                    primaryFiles.Add(new FileDeploymentInfo(item.SourcePath, item.PackageName, DeploymentLocation.Archive));
                                    break;
                                case ArtifactOrigin.Runtime:
                                    runtimeFiles.Add(new FileDeploymentInfo(item.SourcePath, item.PackageName, DeploymentLocation.Archive));
                                    break;
                            }

                            deployedPackages.Add(item.PackageName);
                        }

                        break;
                }
            }

            foreach (var packageName in closure.Packages)
            {
                if (!deployedPackages.Contains(packageName))
                {
                    filteredPackages.Add(packageName);
                }
            }

            var strategy = _environment.Platform.Family == PlatformFamily.Windows
                ? DeploymentStrategy.DirectCopy
                : DeploymentStrategy.Archive;

            var statistics = new DeploymentStatistics(
                currentLibraryName,
                primaryFiles,
                runtimeFiles,
                licenseFiles,
                deployedPackages,
                filteredPackages,
                strategy);

            _log.Information("Created deployment plan for {0}: {1} primary, {2} runtime, {3} license files via {4}.",
                currentLibraryName, primaryFiles.Count, runtimeFiles.Count, licenseFiles.Count, strategy);

            return new DeploymentPlan(actions, statistics);
        }
        catch (OperationCanceledException)
        {
            _log.Warning("Artifact planning was canceled for {0}.", current.Name);
            throw;
        }
        catch (Exception ex)
        {
            var message = $"Error while planning artifacts for {current.Name}: {ex.Message}";
            _log.Error(message);

            return new ArtifactPlannerError(message);
        }
    }

    private static bool IsLicense(FilePath f) =>
        f.Segments.Contains("share", StringComparer.OrdinalIgnoreCase) &&
        f.GetFilename().FullPath.Equals("copyright", StringComparison.OrdinalIgnoreCase);
}
