using System;
using System.Linq;
using System.Threading.Tasks;
using Build.Context;
using Build.Context.Models;
using Build.Modules.Vcpkg;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Frosting;
using Spectre.Console;

namespace Build.Tasks.Vcpkg;

[TaskName("Harvest")]
public sealed class HarvestTask : AsyncFrostingTask<BuildContext>
{
    private readonly VcpkgHarvesterService _harvesterService;
    private readonly ManifestConfig _manifestConfig;

    public HarvestTask(VcpkgHarvesterService harvesterService, ManifestConfig manifestConfig)
    {
        _harvesterService = harvesterService ?? throw new ArgumentNullException(nameof(harvesterService));
        _manifestConfig = manifestConfig ?? throw new ArgumentNullException(nameof(manifestConfig));
    }

    public override async Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var librariesToProcess = context.Vcpkg.Libraries;
        var rid = context.Vcpkg.Rid ?? context.Environment.Platform.Rid();

        if (!librariesToProcess.Any())
        {
            context.Log.Warning("No libraries specified for harvest.");
            return;
        }

        // Define a base output directory for harvested artifacts
        // This path should ideally be managed by PathService or configurable
        var harvestOutputBaseDirectory = context.Directory("build/_harvest_output");
        context.EnsureDirectoryExists(harvestOutputBaseDirectory);

        context.Log.Information("Harvesting for RID: {0}", rid);
        context.Log.Information("Output directory: {0}", harvestOutputBaseDirectory.FullPath);

        var allSucceeded = true;

        foreach (var libName in librariesToProcess)
        {
            var libraryManifest = _manifestConfig.LibraryManifests.FirstOrDefault(m =>
                string.Equals(m.Name, libName, StringComparison.OrdinalIgnoreCase));

            if (libraryManifest == null)
            {
                context.Log.Error("Library '{0}' not found in manifest. Skipping.", libName);
                allSucceeded = false;
                continue;
            }

            context.Log.Information("--- Starting harvest for: {0} ---", libraryManifest.Name);
            var report = await _harvesterService.HarvestAsync(libraryManifest, rid, harvestOutputBaseDirectory, context.CancellationToken);

            if (report != null)
            {
                context.Log.Information("Harvest successful for '{0}': Copied {1} artifacts from {2} Vcpkg packages.",
                    libraryManifest.Name, report.Artifacts.Count, report.ProcessedPackageNames.Count);
                // TODO: Optionally display more details from the report, e.g., list of artifacts
                // DisplayHarvestReportSummary(context, report, libraryManifest.Name);
            }
            else
            {
                context.Log.Error("Harvest failed for '{0}'.", libraryManifest.Name);
                allSucceeded = false;
            }
            context.Log.Information("--- Finished harvest for: {0} ---", libraryManifest.Name);
        }

        if (!allSucceeded)
        {
            throw new CakeException("One or more libraries failed to harvest. Check logs for details.");
        }
        context.Log.Information("All specified libraries harvested successfully.");
    }

    // Optional: A new helper to display summary or details from the HarvestReport
    // private static void DisplayHarvestReportSummary(BuildContext context, HarvestReport report, string libraryName)
    // {
    //     AnsiConsole.MarkupLine($"[green]Summary for {libraryName}:[/]");
    //     AnsiConsole.MarkupLine($"  Root Binary: {report.RootBinary.GetFilename().FullPath}");
    //     AnsiConsole.MarkupLine($"  Total Artifacts: {report.Artifacts.Count}");
    //     AnsiConsole.MarkupLine($"  Processed Vcpkg Packages: {report.ProcessedPackageNames.Count} ({string.Join(", ", report.ProcessedPackageNames)})");
    //     // Could add a table for artifacts if desired, similar to old DisplayCollectedDependencies
    // }
}
