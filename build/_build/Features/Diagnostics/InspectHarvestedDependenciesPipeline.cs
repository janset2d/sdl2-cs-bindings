using System.Globalization;
using Build.Host;
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

namespace Build.Features.Diagnostics;

/// <summary>
/// Per-RID diagnostic: for each library in scope, extract (Unix) or read (Windows) the harvested
/// payload for the active RID, locate the primary binary via <see cref="PrimaryBinary"/> patterns,
/// and invoke the platform dependency scanner (Dumpbin / Ldd / Otool). Replaces the bash
/// <c>inspect_ldd</c> loop in <c>docs/playbook/TEMP-wsl-smoke-commands.md</c> §5.
/// </summary>
public sealed class InspectHarvestedDependenciesPipeline(
    ICakeContext cakeContext,
    ICakeLog log,
    IPathService pathService,
    IRuntimeProfile runtimeProfile,
    ManifestConfig manifestConfig)
{
    private readonly ICakeContext _cakeContext = cakeContext ?? throw new ArgumentNullException(nameof(cakeContext));
    private readonly ICakeLog _log = log ?? throw new ArgumentNullException(nameof(log));
    private readonly IPathService _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));
    private readonly IRuntimeProfile _runtimeProfile = runtimeProfile ?? throw new ArgumentNullException(nameof(runtimeProfile));
    private readonly ManifestConfig _manifestConfig = manifestConfig ?? throw new ArgumentNullException(nameof(manifestConfig));

    public Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var rid = _runtimeProfile.Rid;
        var platform = _runtimeProfile.Family;
        var osKey = ResolveOsKey(platform, rid);

        var libraries = ResolveLibraries(context);

        foreach (var library in libraries)
        {
            InspectLibrary(library, rid, platform, osKey);
        }

        _log.Information("Inspect-HarvestedDependencies completed for RID '{0}' ({1} libraries).", rid, libraries.Count);
        return Task.CompletedTask;
    }

    private List<LibraryManifest> ResolveLibraries(BuildContext context)
    {
        var specified = context.Options.Vcpkg.Libraries;
        var manifestLibs = _manifestConfig.LibraryManifests.ToList();

        if (specified.Count == 0)
        {
            return manifestLibs;
        }

        var result = new List<LibraryManifest>(specified.Count);
        foreach (var name in specified)
        {
            var match = manifestLibs.SingleOrDefault(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase))
                        ?? throw new CakeException($"Library '{name}' was requested via --library but is missing in manifest.json.");
            result.Add(match);
        }

        return result;
    }

    private static string ResolveOsKey(RuntimeFamily platform, string rid)
    {
        return platform switch
        {
            RuntimeFamily.Windows => "Windows",
            RuntimeFamily.Linux => "Linux",
            RuntimeFamily.OSX => "OSX",
            _ => throw new CakeException(
                $"Inspect-HarvestedDependencies cannot derive OS key from platform '{platform}' (RID='{rid}').")
        };
    }

    private void InspectLibrary(LibraryManifest library, string rid, RuntimeFamily platform, string osKey)
    {
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
    }

    private DirectoryPath PrepareInspectionDirectory(
        LibraryManifest library,
        string rid,
        RuntimeFamily platform,
        DirectoryPath harvestNativeDir)
    {
        if (platform == RuntimeFamily.Windows)
        {
            // Windows ships primaries uncompressed directly under runtimes/win-x64/native/.
            // No extraction required; scanner runs against the harvest dir itself.
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
                    ?? throw new CakeException(
                        $"Library '{library.Name}' has no primary_binaries entry for OS '{osKey}'.");

        return [.. entry.Patterns];
    }

    private FilePath ResolvePrimaryBinary(
        DirectoryPath inspectedDir,
        List<string> patterns,
        string libraryName)
    {
        foreach (var pattern in patterns)
        {
            var globExpr = string.Create(CultureInfo.InvariantCulture, $"{inspectedDir.FullPath}/**/{pattern}");
            var matches = _cakeContext.GetFiles(globExpr);
            if (matches.Count > 0)
            {
                // Deterministic pick: shortest path wins (top-of-tree over nested duplicates).
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
                throw new CakeException(
                    $"Inspect scanner dispatch does not cover platform '{platform}' (library '{libraryName}').");
        }
    }

    private void LogDependencyMap(string libraryName, string scanner, IReadOnlyDictionary<string, string> deps)
    {
        if (deps.Count == 0)
        {
            _log.Warning("[{0}] {1}: no dependencies reported.", libraryName, scanner);
            return;
        }

        _log.Information("[{0}] {1} ({2} deps):", libraryName, scanner, deps.Count);
        foreach (var (soname, resolved) in deps.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            _log.Information("    {0} => {1}", soname, resolved);
        }
    }
}
