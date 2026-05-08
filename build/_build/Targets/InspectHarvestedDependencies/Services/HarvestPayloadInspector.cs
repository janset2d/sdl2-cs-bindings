using System.Globalization;
using Build.Host.Paths;
using Build.Shared.Manifest;
using Build.Shared.Runtime;
using Build.Tools.Dumpbin;
using Build.Tools.Ldd;
using Build.Tools.Otool;
using Build.Tools.Tar;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;

namespace Build.Targets.InspectHarvestedDependencies.Services;

public sealed class HarvestPayloadInspector(
    ICakeContext cakeContext,
    ICakeLog log,
    IPathService pathService)
{
    private readonly ICakeContext _cakeContext = cakeContext ?? throw new ArgumentNullException(nameof(cakeContext));
    private readonly ICakeLog _log = log ?? throw new ArgumentNullException(nameof(log));
    private readonly IPathService _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));

    public Task InspectAsync(LibraryManifest library, RuntimeFamily platform, string rid, string osKey)
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentException.ThrowIfNullOrWhiteSpace(rid);
        ArgumentException.ThrowIfNullOrWhiteSpace(osKey);

        var harvestNativeDir = _pathService
            .GetHarvestLibraryRidRuntimesDir(library.Name, rid)
            .Combine("native");

        if (!_cakeContext.DirectoryExists(harvestNativeDir))
        {
            throw new CakeException(
                $"Inspect precondition failed: '{harvestNativeDir.FullPath}' missing for library '{library.Name}'. " +
                $"Run '--target Harvest --rid {rid}' first.");
        }

        var inspectedDir = PrepareInspectionDirectory(library, rid, platform, harvestNativeDir);
        var patterns = ResolvePatterns(library, osKey);
        var primary = ResolvePrimaryBinary(inspectedDir, patterns, library.Name);

        _log.Information("[{0}] Primary binary resolved: {1}", library.Name, primary.FullPath);
        InvokePlatformScanner(library.Name, platform, primary);
        return Task.CompletedTask;
    }

    private DirectoryPath PrepareInspectionDirectory(
        LibraryManifest library,
        string rid,
        RuntimeFamily platform,
        DirectoryPath harvestNativeDir)
    {
        if (platform == RuntimeFamily.Windows)
        {
            return harvestNativeDir;
        }

        var destination = _pathService.GetInspectOutputLibraryDir(rid, library.Name);
        if (_cakeContext.DirectoryExists(destination))
        {
            _cakeContext.DeleteDirectory(destination, new DeleteDirectorySettings { Recursive = true, Force = true });
        }

        _cakeContext.CreateDirectory(destination);

        var archive = harvestNativeDir.CombineWithFilePath("native.tar.gz");
        if (!_cakeContext.FileExists(archive))
        {
            throw new CakeException(
                $"Inspect precondition failed: Unix harvest tarball missing at '{archive.FullPath}' for library '{library.Name}'. " +
                "Harvest on Unix must produce native.tar.gz — see ArtifactPlanner.");
        }

        _log.Information("[{0}] Extracting '{1}' -> '{2}'", library.Name, archive.FullPath, destination.FullPath);
        _cakeContext.TarExtract(new TarExtractSettings(archive, destination));

        return destination;
    }

    private static List<string> ResolvePatterns(LibraryManifest library, string osKey)
    {
        var entry = library.PrimaryBinaries.SingleOrDefault(p => string.Equals(p.Os, osKey, StringComparison.OrdinalIgnoreCase))
                    ?? throw new CakeException($"Library '{library.Name}' has no primary_binaries entry for OS '{osKey}'.");

        return [.. entry.Patterns];
    }

    private FilePath ResolvePrimaryBinary(DirectoryPath inspectedDir, List<string> patterns, string libraryName)
    {
        foreach (var pattern in patterns)
        {
            var globExpr = string.Create(CultureInfo.InvariantCulture, $"{inspectedDir.FullPath}/**/{pattern}");
            var matches = _cakeContext.GetFiles(globExpr);
            if (matches.Count > 0)
            {
                return matches.OrderBy(f => f.FullPath.Length).First();
            }
        }

        throw new CakeException(
            $"Inspect failed: no primary binary matched patterns [{string.Join(", ", patterns)}] under '{inspectedDir.FullPath}' for library '{libraryName}'.");
    }

    private void InvokePlatformScanner(string libraryName, RuntimeFamily platform, FilePath primary)
    {
        switch (platform)
        {
            case RuntimeFamily.Windows:
            {
                var output = _cakeContext.DumpbinDependents(new DumpbinDependentsSettings(primary.FullPath));
                _log.Information("[{0}] dumpbin /dependents:{1}{2}", libraryName, Environment.NewLine, output ?? "(no output)");
                break;
            }

            case RuntimeFamily.Linux:
            {
                var deps = _cakeContext.LddDependencies(new LddSettings(primary));
                LogDependencyMap(libraryName, "ldd", deps);
                break;
            }

            case RuntimeFamily.OSX:
            {
                var deps = _cakeContext.OtoolDependencies(new OtoolSettings(primary));
                LogDependencyMap(libraryName, "otool -L", deps);
                break;
            }

            default:
                throw new CakeException($"Inspect scanner dispatch does not cover platform '{platform}' (library '{libraryName}').");
        }

        void LogDependencyMap(string libName, string scanner, IReadOnlyDictionary<string, string> deps)
        {
            if (deps.Count == 0)
            {
                _log.Warning("[{0}] {1}: no dependencies reported.", libName, scanner);
                return;
            }

            _log.Information("[{0}] {1} ({2} deps):", libName, scanner, deps.Count);

            foreach (var (soname, resolved) in deps.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            {
                _log.Information("    {0} => {1}", soname, resolved);
            }
        }
    }
}
