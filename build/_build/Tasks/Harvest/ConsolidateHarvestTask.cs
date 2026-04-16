#pragma warning disable CA1031

using Build.Context;
using Build.Modules.Harvesting.Models;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Cake.Frosting;
using System.Text.Json;

namespace Build.Tasks.Harvest;

[TaskName("ConsolidateHarvest")]
[IsDependentOn(typeof(HarvestTask))]
public sealed class ConsolidateHarvestTask : AsyncFrostingTask<BuildContext>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public override async Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var harvestOutputBase = context.Paths.HarvestOutput;
        context.Log.Information("Consolidating harvest RID status files from: {0}", harvestOutputBase);

        if (!context.DirectoryExists(harvestOutputBase))
        {
            context.Log.Warning("Harvest output directory does not exist: {0}", harvestOutputBase);
            return;
        }

        // Get all library directories using glob pattern
        var libraryDirs = context.GetDirectories($"{harvestOutputBase}/*");

        if (libraryDirs.Count == 0)
        {
            context.Log.Warning("No library directories found in harvest output");
            return;
        }

        foreach (var libraryDir in libraryDirs)
        {
            var libraryName = libraryDir.GetDirectoryName();
            await ConsolidateLibraryAsync(context, libraryDir, libraryName);
        }

        context.Log.Information("Harvest consolidation completed successfully");
    }

    private static async Task ConsolidateLibraryAsync(BuildContext context, DirectoryPath libraryDir, string libraryName)
    {
        try
        {
            var ridStatusDir = libraryDir.Combine("rid-status");

            if (!context.DirectoryExists(ridStatusDir))
            {
                context.Log.Information("No RID status directory found for library: {0}", libraryName);
                return;
            }

            // Get all RID status files using glob pattern
            var ridStatusFiles = context.GetFiles($"{ridStatusDir}/*.json");

            if (ridStatusFiles.Count == 0)
            {
                context.Log.Warning("No RID status files found for library: {0}", libraryName);
                return;
            }

            context.Log.Information("Found {0} RID status files for library: {1}", ridStatusFiles.Count, libraryName);

            var ridStatuses = new List<RidHarvestStatus>();

            // Load all RID status files
            foreach (var statusFile in ridStatusFiles)
            {
                try
                {
                    var jsonContent = await context.ReadAllTextAsync(statusFile);
                    var ridStatus = JsonSerializer.Deserialize<RidHarvestStatus>(jsonContent, JsonOptions);

                    if (ridStatus != null)
                    {
                        ridStatuses.Add(ridStatus);
                        context.Log.Verbose("Loaded RID status for {0}: {1}", ridStatus.Rid, ridStatus.Success ? "Success" : "Failed");
                    }
                }
                catch (Exception ex)
                {
                    context.Log.Warning("Failed to parse RID status file {0}: {1}", statusFile.GetFilename(), ex.Message);
                }
            }

            if (ridStatuses.Count == 0)
            {
                context.Log.Warning("No valid RID status data found for library: {0}", libraryName);
                return;
            }

            // Generate consolidation manifest
            var manifest = GenerateHarvestManifest(libraryName, ridStatuses);

            // Write harvest manifest
            var manifestPath = libraryDir.CombineWithFilePath("harvest-manifest.json");
            var manifestJson = JsonSerializer.Serialize(manifest, JsonOptions);
            await context.WriteAllTextAsync(manifestPath, manifestJson);

            context.Log.Information("Generated harvest manifest for {0}: {1}", libraryName, manifestPath);

            // Generate separate summary file
            var summaryPath = libraryDir.CombineWithFilePath("harvest-summary.json");
            var summaryJson = JsonSerializer.Serialize(manifest.Summary, JsonOptions);
            await context.WriteAllTextAsync(summaryPath, summaryJson);

            context.Log.Information("Generated harvest summary for {0}: {1}", libraryName, summaryPath);
        }
        catch (Exception ex)
        {
            context.Log.Error("Failed to consolidate harvest for library {0}: {1}", libraryName, ex.Message);
            context.Log.Verbose("Consolidation error details: {0}", ex);
        }
    }

    private static HarvestManifest GenerateHarvestManifest(string libraryName, List<RidHarvestStatus> ridStatuses)
    {
        var successfulRids = ridStatuses.Where(r => r.Success).ToList();
        var failedRids = ridStatuses.Where(r => !r.Success).ToList();

        var summary = new HarvestSummary
        {
            TotalRids = ridStatuses.Count,
            SuccessfulRids = successfulRids.Count,
            FailedRids = failedRids.Count,
            SuccessRate = ridStatuses.Count > 0 ? (double)successfulRids.Count / ridStatuses.Count : 0.0,
        };

        return new HarvestManifest
        {
            LibraryName = libraryName,
            GeneratedTimestamp = DateTimeOffset.UtcNow,
            Rids = ridStatuses.AsReadOnly(),
            Summary = summary,
        };
    }
}
