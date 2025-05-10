namespace Build.Modules.Vcpkg;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Build.Context;
using Build.Context.Models;
using Build.Modules.DependencyAnalysis;
using Build.Modules.Harvesting.Models;
using Build.Modules.Vcpkg.Models;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;

public sealed class VcpkgHarvesterService // Not making it internal for now, can be adjusted
{
    private readonly IRuntimeScanner _runtimeScanner;
    private readonly IPackageInfoProvider _packageInfoProvider;
    // private readonly IFilesystemCopier _copier; // To be added when file copying is implemented
    private readonly ICakeLog _log;
    private readonly SystemArtefactsConfig _systemArtefactsConfig;
    private readonly ManifestConfig _manifestConfig;
    private readonly PathService _pathService;
    private readonly RuntimeConfig _runtimeConfig;

    public VcpkgHarvesterService(
        IRuntimeScanner runtimeScanner,
        IPackageInfoProvider packageInfoProvider,
        ICakeLog log,
        SystemArtefactsConfig systemArtefactsConfig,
        ManifestConfig manifestConfig,
        PathService pathService,
        RuntimeConfig runtimeConfig)
    {
        _runtimeScanner = runtimeScanner ?? throw new ArgumentNullException(nameof(runtimeScanner));
        _packageInfoProvider = packageInfoProvider ?? throw new ArgumentNullException(nameof(packageInfoProvider));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _systemArtefactsConfig = systemArtefactsConfig ?? throw new ArgumentNullException(nameof(systemArtefactsConfig));
        _manifestConfig = manifestConfig ?? throw new ArgumentNullException(nameof(manifestConfig));
        _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));
        _runtimeConfig = runtimeConfig ?? throw new ArgumentNullException(nameof(runtimeConfig));
    }

    public async Task<HarvestReport?> HarvestAsync(
        LibraryManifest libraryToHarvest,
        string rid,
        DirectoryPath harvestOutputBaseDirectory, // e.g., build/artifacts/harvest
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(libraryToHarvest);
        ArgumentException.ThrowIfNullOrEmpty(rid);
        ArgumentNullException.ThrowIfNull(harvestOutputBaseDirectory);

        _log.Information("Starting harvest for library '{0}' on RID '{1}'...", libraryToHarvest.Name, rid);

        var todoBins = new Queue<FilePath>();
        var processedBins = new HashSet<FilePath>(PathEqualityComparer.Default); // Using the one from WindowsDumpbinScanner
        var processedPackageNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var collectedArtifacts = new HashSet<NativeArtifact>();

        string? coreLibPlatformName = DetermineCoreLibPlatformName(rid);

        // 1. Get and process the primary binary for the libraryToHarvest
        var primaryBinaryPath = await GetPathForPrimaryBinaryAsync(libraryToHarvest, rid, ct);
        if (primaryBinaryPath == null)
        {
            _log.Error("Could not determine primary binary path for '{0}' on RID '{1}'. Aborting harvest for this library.", libraryToHarvest.Name, rid);
            return null;
        }

        var primaryPackageInfo = await _packageInfoProvider.GetPackageInfoForFileAsync(primaryBinaryPath, ct);
        if (primaryPackageInfo == null)
        {
            _log.Error("Could not get package info for primary binary '{0}' of library '{1}'. Aborting harvest for this library.", primaryBinaryPath, libraryToHarvest.Name);
            return null;
        }

        todoBins.Enqueue(primaryBinaryPath);
        collectedArtifacts.Add(CreateNativeArtifact(primaryBinaryPath, primaryPackageInfo.PortName, ArtifactOrigin.Primary, rid, harvestOutputBaseDirectory));
        // Add all owned files of the primary package initially
        AddOwnedFilesAsArtifacts(primaryPackageInfo, collectedArtifacts, rid, harvestOutputBaseDirectory, isPrimaryPackage: true, primaryBinaryPath);
        processedPackageNames.Add(primaryPackageInfo.PortName);

        // Process declared dependencies of the primary package
        await ProcessDeclaredDependenciesAsync(primaryPackageInfo, todoBins, processedPackageNames, collectedArtifacts, rid, harvestOutputBaseDirectory, processedBins, ct);

        // Main processing loop
        while (todoBins.TryDequeue(out var currentBin))
        {
            ct.ThrowIfCancellationRequested();

            if (!processedBins.Add(currentBin))
            {
                _log.Verbose("Binary '{0}' already processed or queued. Skipping.", currentBin.GetFilename());
                continue;
            }

            if (IsSystemFile(currentBin, rid)) // Adjusted IsSystemDll to IsSystemFile for broader use
            {
                _log.Verbose("Skipping system file: {0}", currentBin.GetFilename());
                continue;
            }

            // Special skip for core library when processing a satellite library's dependencies
            if (!libraryToHarvest.CoreLib &&
                !string.IsNullOrEmpty(coreLibPlatformName) &&
                currentBin.GetFilename().FullPath.Equals(coreLibPlatformName, StringComparison.OrdinalIgnoreCase))
            {
                _log.Verbose("Skipping deep scan of core library '{0}' while processing satellite library '{1}'.", coreLibPlatformName, libraryToHarvest.Name);
                // We still want to collect the core lib itself if it was a runtime dep, so it should have been added by AddOwnedFilesAsArtifacts or from runtime scan path
                // The main purpose of this skip is to avoid re-processing all *its* dependencies deeply again.
                continue;
            }

            _log.Debug("Processing binary: {0}", currentBin.GetFilename());

            // Get package info for the current binary
            var currentBinPackageInfo = await _packageInfoProvider.GetPackageInfoForFileAsync(currentBin, ct);
            if (currentBinPackageInfo != null)
            {
                if (processedPackageNames.Add(currentBinPackageInfo.PortName))
                {
                    _log.Debug("New package discovered: {0}", currentBinPackageInfo.PortName);
                    AddOwnedFilesAsArtifacts(currentBinPackageInfo, collectedArtifacts, rid, harvestOutputBaseDirectory, isPrimaryPackage: false, currentBin);
                    await ProcessDeclaredDependenciesAsync(currentBinPackageInfo, todoBins, processedPackageNames, collectedArtifacts, rid, harvestOutputBaseDirectory, processedBins, ct);
                }
            }
            else
            {
                _log.Warning("Could not determine Vcpkg package for binary: {0}. Runtime dependencies will be harvested but package association may be lost for some artifacts.", currentBin.GetFilename());
                // If we can't get package info, we can't get its declared dependencies via metadata.
                // We still add the currentBin itself if it wasn't added (e.g. if it was a runtime dep from an unknown package)
                if (!collectedArtifacts.Any(a => a.SourcePath.Equals(currentBin)))
                {
                     collectedArtifacts.Add(CreateNativeArtifact(currentBin, "Unknown", ArtifactOrigin.Runtime, rid, harvestOutputBaseDirectory));
                }
            }

            // Runtime Scan for currentBin
            _log.Debug("Performing runtime scan for: {0}", currentBin.GetFilename());
            var runtimeDeps = await _runtimeScanner.ScanAsync(currentBin, ct);
            foreach (var depPath in runtimeDeps)
            {
                if (!processedBins.Contains(depPath) && !todoBins.Contains(depPath)) // Avoid re-queuing if already seen or to be seen
                {
                    _log.Debug("Runtime dependency found: '{0}' for '{1}'. Enqueuing.", depPath.GetFilename(), currentBin.GetFilename());
                    todoBins.Enqueue(depPath);

                    // Add runtime dependency as an artifact. Its package might be unknown until it's processed from the queue.
                    if (!collectedArtifacts.Any(a => a.SourcePath.Equals(depPath)))
                    {
                        // We need to determine its package. If GetPackageInfoForFileAsync was called for depPath when it's processed,
                        // then the artifact added then will have the correct package. Here, we might add it as "Unknown".
                        // This logic depends on whether all files from todoBin get their package info resolved.
                        // For now, let's assume it will be updated/added correctly when depPath itself is processed.
                    }
                }
            }
        }

        _log.Information("Harvest completed for '{0}'. Found {1} artifacts from {2} Vcpkg packages.",
            libraryToHarvest.Name, collectedArtifacts.Count, processedPackageNames.Count);

        return new HarvestReport(primaryBinaryPath, collectedArtifacts.ToImmutableHashSet(), processedPackageNames.ToImmutableHashSet());
    }

    private static NativeArtifact CreateNativeArtifact(FilePath sourcePath, string packageName, ArtifactOrigin origin, string rid, DirectoryPath harvestOutputBaseDirectory)
    {
        // Define target path structure, e.g., <outputDir>/<rid>/native/<filename>
        // Or, if it's a license: <outputDir>/<rid>/licenses/<packageName>/<filename>
        DirectoryPath targetDirectory;
        if (origin == ArtifactOrigin.License)
        {
            targetDirectory = harvestOutputBaseDirectory.Combine(rid).Combine("licenses").Combine(packageName);
        }
        else
        {
            targetDirectory = harvestOutputBaseDirectory.Combine(rid).Combine("native");
        }
        var targetPath = targetDirectory.CombineWithFilePath(sourcePath.GetFilename());

        return new NativeArtifact(
            FileName: sourcePath.GetFilename().FullPath,
            SourcePath: sourcePath,
            TargetPath: targetPath,
            PackageName: packageName,
            Origin: origin
        );
    }

    private void AddOwnedFilesAsArtifacts(
        PackageInfo packageInfo,
        HashSet<NativeArtifact> collectedArtifacts,
        string rid,
        DirectoryPath harvestOutputBaseDirectory,
        bool isPrimaryPackage,
        FilePath? primaryOrCurrentBinPath) // Used to mark the primary binary with correct origin
    {
        foreach (var ownedFile in packageInfo.OwnedFiles)
        {
            if (collectedArtifacts.Any(a => a.SourcePath.Equals(ownedFile))) continue; // Already added

            ArtifactOrigin origin; // Declare, but don't assign default if all paths assign it
            if (isPrimaryPackage && primaryOrCurrentBinPath != null && ownedFile.Equals(primaryOrCurrentBinPath))
            {
                origin = ArtifactOrigin.Primary;
            }
            else if (ownedFile.GetExtension().Equals(".dll", StringComparison.OrdinalIgnoreCase) ||
                     ownedFile.GetExtension().Equals(".so", StringComparison.OrdinalIgnoreCase) ||
                     ownedFile.GetExtension().Equals(".dylib", StringComparison.OrdinalIgnoreCase))
            {
                origin = ArtifactOrigin.Metadata;
            }
            else if (ownedFile.Segments.Contains("share", StringComparer.OrdinalIgnoreCase) &&
                     ownedFile.GetFilename().FullPath.Equals("copyright", StringComparison.OrdinalIgnoreCase))
            {
                origin = ArtifactOrigin.License;
            }
            else
            {
                // Could be headers, .lib files, .pdb files etc. We might not want all of these as artifacts for runtime package.
                // For now, skipping non-binary/non-license files from general metadata unless explicitly handled.
                _log.Verbose("Skipping non-binary/non-license owned file from metadata: {0}", ownedFile.GetFilename());
                continue;
            }
            collectedArtifacts.Add(CreateNativeArtifact(ownedFile, packageInfo.PortName, origin, rid, harvestOutputBaseDirectory));
        }
    }

    private async Task ProcessDeclaredDependenciesAsync(
        PackageInfo currentPackageInfo,
        Queue<FilePath> todoBins,
        HashSet<string> processedPackageNames,
        HashSet<NativeArtifact> collectedArtifacts,
        string rid,
        DirectoryPath harvestOutputBaseDirectory,
        HashSet<FilePath> processedBins,
        CancellationToken ct)
    {
        foreach (var depPackageKey in currentPackageInfo.DeclaredDependencies)
        {
            ct.ThrowIfCancellationRequested();
            // depPackageKey is like "libname:triplet"
            var parts = depPackageKey.Split(':');
            if (parts.Length != 2)
            {
                _log.Warning("Invalid declared dependency format: {0}", depPackageKey);
                continue;
            }
            string depPackageName = parts[0];
            string depTriplet = parts[1]; // This should ideally match the current RID's triplet.
                                         // Vcpkg manifest mode usually ensures compatible triplets for dependencies.

            if (processedPackageNames.Contains(depPackageName))
            {
                _log.Verbose("Declared dependency package '{0}' already processed. Skipping re-processing its owned files and further declared dependencies.", depPackageName);
                continue;
            }

            var depPackageFullInfo = await _packageInfoProvider.GetPackageInfoAsync(depPackageName, depTriplet, ct);
            if (depPackageFullInfo == null) // Merged if
            {
                 _log.Warning("Could not get package info for declared dependency: {0}:{1}", depPackageName, depTriplet);
                 continue; // Skip this dependency if info can't be retrieved
            }

            _log.Debug("Processing declared dependency package: {0}", depPackageName);
            AddOwnedFilesAsArtifacts(depPackageFullInfo, collectedArtifacts, rid, harvestOutputBaseDirectory, isPrimaryPackage: false, primaryOrCurrentBinPath: null);
            foreach(var ownedFile in depPackageFullInfo.OwnedFiles)
            {
                if (ownedFile.GetExtension().Equals(".dll", StringComparison.OrdinalIgnoreCase) ||
                    ownedFile.GetExtension().Equals(".so", StringComparison.OrdinalIgnoreCase) ||
                    ownedFile.GetExtension().Equals(".dylib", StringComparison.OrdinalIgnoreCase))
                {
                    if (!processedBins.Contains(ownedFile) && !todoBins.Contains(ownedFile))
                    {
                        todoBins.Enqueue(ownedFile);
                    }
                }
            }
        }
    }

    private string? DetermineCoreLibPlatformName(string rid)
    {
        var coreLibManifest = _manifestConfig.LibraryManifests.FirstOrDefault(lm => lm.CoreLib);
        if (coreLibManifest != null)
        {
            return GetLibNameForPlatform(coreLibManifest, rid);
        }
        return null;
    }

    private static string? GetLibNameForPlatform(LibraryManifest libraryManifest, string rid)
    {
        string osFamily;
        if (rid.StartsWith("win-", StringComparison.OrdinalIgnoreCase)) osFamily = "Windows";
        else if (rid.StartsWith("osx-", StringComparison.OrdinalIgnoreCase)) osFamily = "OSX";
        else if (rid.StartsWith("linux-", StringComparison.OrdinalIgnoreCase)) osFamily = "Linux";
        else return null;

        return libraryManifest.LibNames
            .FirstOrDefault(ln => string.Equals(ln.Os, osFamily, StringComparison.OrdinalIgnoreCase))?.Name;
    }

    // Helper: Get path for the primary binary of a library manifest
    private async Task<FilePath?> GetPathForPrimaryBinaryAsync(LibraryManifest libraryManifest, string rid, CancellationToken ct)
    {
        var libNameForPlatform = GetLibNameForPlatform(libraryManifest, rid);
        if (string.IsNullOrEmpty(libNameForPlatform))
        {
            _log.Error("Cannot find platform-specific lib name for '{0}' on RID '{1}' in manifest.", libraryManifest.Name, rid);
            return null;
        }

        var packageInfo = await _packageInfoProvider.GetPackageInfoAsync(libraryManifest.VcpkgName, DetermineTripletForRid(rid), ct);
        if (packageInfo == null)
        {
            _log.Error("Cannot get package info for '{0}' (triplet: {1}) to find its primary binary '{2}'.", libraryManifest.VcpkgName, DetermineTripletForRid(rid), libNameForPlatform);
            return null;
        }

        var primaryBinary = packageInfo.OwnedFiles
            .FirstOrDefault(f => f.GetFilename().FullPath.Equals(libNameForPlatform, StringComparison.OrdinalIgnoreCase) &&
                                 (f.Segments.Any(s => s.Equals("bin", StringComparison.OrdinalIgnoreCase)) ||
                                  f.Segments.Any(s => s.Equals("lib", StringComparison.OrdinalIgnoreCase)))); // lib for .so often in /lib

        if (primaryBinary == null)
        {
             _log.Error("Primary binary '{0}' not found in owned files of package '{1}' (triplet: {2}).", libNameForPlatform, libraryManifest.VcpkgName, DetermineTripletForRid(rid));
        }
        return primaryBinary;
    }

    private string DetermineTripletForRid(string rid)
    {
        var runtimeInfo = _runtimeConfig.Runtimes.SingleOrDefault(r => r.Rid == rid);
        if (runtimeInfo == null)
        {
            throw new InvalidOperationException($"RID '{rid}' not found in runtime configuration.");
        }
        return runtimeInfo.Triplet;
    }

    // Helper: Check if a file is a system file based on SystemArtefactsConfig
    private bool IsSystemFile(FilePath filePath, string rid)
    {
        var fileName = filePath.GetFilename().FullPath;
        ImmutableList<string> systemPatterns = ImmutableList<string>.Empty;

        if (rid.StartsWith("win-", StringComparison.OrdinalIgnoreCase) && _systemArtefactsConfig.Windows != null)
        {
            systemPatterns = _systemArtefactsConfig.Windows.SystemDlls;
        }
        else if (rid.StartsWith("linux-", StringComparison.OrdinalIgnoreCase) && _systemArtefactsConfig.Linux != null)
        {
            systemPatterns = _systemArtefactsConfig.Linux.SystemLibraries;
        }
        else if (rid.StartsWith("osx-", StringComparison.OrdinalIgnoreCase) && _systemArtefactsConfig.Osx != null)
        {
            systemPatterns = _systemArtefactsConfig.Osx.SystemLibraries;
        }

        foreach (var systemPattern in systemPatterns)
        {
            if (systemPattern.Contains('*', StringComparison.Ordinal))
            {
                var regexPattern = "^" + Regex.Escape(systemPattern).Replace("\\*", ".*", StringComparison.OrdinalIgnoreCase) + "$";
                if (Regex.IsMatch(fileName, regexPattern, RegexOptions.IgnoreCase))
                {
                    return true;
                }
            }
            else if (string.Equals(fileName, systemPattern, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}
