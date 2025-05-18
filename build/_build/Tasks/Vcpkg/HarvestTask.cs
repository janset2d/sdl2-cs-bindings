#pragma warning disable CA1031

using System.Collections.Immutable;
using Build.Context;
using Build.Context.Models;
using Build.Modules.Harvesting;
using Build.Modules.Harvesting.Models;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Frosting;
using Spectre.Console;

namespace Build.Tasks.Vcpkg;

[TaskName("Harvest")]
public sealed class HarvestTask : AsyncFrostingTask<BuildContext>
{
    private readonly BinaryClosureWalker _binaryClosureWalker;
    private readonly ArtifactPlanner _artifactPlanner;
    private readonly ManifestConfig _manifestConfig;

    public HarvestTask(BinaryClosureWalker binaryClosureWalker, ArtifactPlanner artifactPlanner, ManifestConfig manifestConfig)
    {
        _binaryClosureWalker = binaryClosureWalker;
        _artifactPlanner = artifactPlanner;
        _manifestConfig = manifestConfig ?? throw new ArgumentNullException(nameof(manifestConfig));
    }

    public override async Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var outputBase = context.Paths.HarvestOutput;
        var librariesToProcess = context.Vcpkg.Libraries;

        if (!librariesToProcess.Any())
        {
            context.Log.Warning("No libraries specified for harvest.");
            return;
        }

        var allSucceeded = true;

        foreach (var library in librariesToProcess)
        {
            var manifest = _manifestConfig.LibraryManifests.SingleOrDefault(m => string.Equals(m.Name, library, StringComparison.OrdinalIgnoreCase));

            if (manifest == null)
            {
                context.Log.Error("Library '{0}' not found in manifest. Skipping.", library);
                allSucceeded = false;
                continue;
            }

            context.Log.Information("--- Starting harvest for: {0} ---", manifest.Name);

            try
            {
                var closure = await _binaryClosureWalker.BuildClosureAsync(manifest);
                if (closure is null)
                {
                    return;
                }

                var plan = await _artifactPlanner.CreatePlanAsync(manifest, closure, outputBase);
                //await _copier.CopyAsync(plan.Artifacts, ct);

                var harvestReport = new HarvestReport(closure.PrimaryBinary, plan.Artifacts.ToImmutableHashSet(), closure.Packages.ToImmutableHashSet());

                DisplayHarvestReportSummary(context, harvestReport, manifest.Name);

                context.Log.Information("--- Finished harvest for: {0} ---", manifest.Name);
            }
            catch (Exception ex)
            {
                context.Log.Error("Harvest failed for '{0}'. Exception Message: {1}", manifest.Name, ex.Message);
                allSucceeded = false;
            }

            if (!allSucceeded)
            {
                throw new CakeException("One or more libraries failed to harvest. Check logs for details.");
            }

            context.Log.Information("All specified libraries harvested successfully.");
        }
    }

    private static void DisplayHarvestReportSummary(BuildContext context, HarvestReport report, string libraryName)
    {
        context.Log.Information("Harvest successful for '{0}': Copied {1} artifacts from {2} Vcpkg packages.",
            libraryName, report.Artifacts.Count, report.ProcessedPackageNames.Count);

        AnsiConsole.MarkupLine($"[green]Summary for {libraryName}:[/]");
        AnsiConsole.MarkupLine($"  Root Binary: {report.RootBinary.GetFilename().FullPath}");
        AnsiConsole.MarkupLine($"  Total Artifacts: {report.Artifacts.Count}");
        AnsiConsole.MarkupLine($"  Processed Vcpkg Packages: {report.ProcessedPackageNames.Count} ({string.Join(", ", report.ProcessedPackageNames)})");
        // Could add a table for artifacts if desired, similar to old DisplayCollectedDependencies
    }
}
