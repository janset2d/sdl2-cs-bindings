using Build.Data.Manifest;
using Build.Data.Manifest.Models;
using Build.Host;
using Build.Host.Runtime;
using Build.Targets.InspectHarvestedDependencies.Services;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Frosting;

namespace Build.Targets.InspectHarvestedDependencies;

[TaskName("Inspect-HarvestedDependencies")]
[TaskDescription("Per-RID dep-scan of harvest payload; extracts Unix tarballs, reads Windows native/ directly, then runs Dumpbin/Ldd/Otool")]
public sealed class InspectHarvestedDependenciesTask(IManifestRepository manifestRepository, HarvestPayloadInspector inspector) : AsyncFrostingTask<BuildContext>
{
    private readonly IManifestRepository _manifestRepository = manifestRepository ?? throw new ArgumentNullException(nameof(manifestRepository));
    private readonly HarvestPayloadInspector _inspector = inspector ?? throw new ArgumentNullException(nameof(inspector));

    public override async Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var manifest = _manifestRepository.Load();
        var libraries = ResolveLibraries(manifest, context.Libraries);
        var rid = context.RuntimeIdentifier;
        var platform = context.Runtime.Family;
        var osKey = ResolveOsKey(platform, rid);

        foreach (var library in libraries)
        {
            await _inspector.InspectAsync(library, platform, rid, osKey);
        }

        context.Log.Information("Inspect-HarvestedDependencies completed for RID '{0}' ({1} libraries).", rid, libraries.Count);
    }

    private static List<LibraryManifest> ResolveLibraries(ManifestConfig manifest, IReadOnlyList<string> requested)
    {
        var manifestLibs = manifest.LibraryManifests.ToList();
        if (requested.Count == 0)
        {
            return manifestLibs;
        }

        var result = new List<LibraryManifest>(requested.Count);
        foreach (var name in requested)
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
            _ => throw new CakeException($"Inspect-HarvestedDependencies cannot derive OS key from platform '{platform}' (RID='{rid}')."),
        };
    }
}
