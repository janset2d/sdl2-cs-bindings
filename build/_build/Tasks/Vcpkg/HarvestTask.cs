#pragma warning disable CA1031, MA0051

using System.Collections.Immutable;
using Build.Context;
using Build.Context.Models;
using Build.Modules.Contracts;
using Build.Modules.Harvesting.Models;
using Build.Modules.Harvesting.Results;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Frosting;
using Spectre.Console;

namespace Build.Tasks.Vcpkg;

[TaskName("Harvest")]
public sealed class HarvestTask : AsyncFrostingTask<BuildContext>
{
    private readonly IBinaryClosureWalker _binaryClosureWalker;
    private readonly IArtifactPlanner _artifactPlanner;
    private readonly IFilesystemCopier _filesystemCopier;
    private readonly ManifestConfig _manifestConfig;

    public HarvestTask(
        IBinaryClosureWalker binaryClosureWalker,
        IArtifactPlanner artifactPlanner,
        IFilesystemCopier filesystemCopier,
        ManifestConfig manifestConfig)
    {
        _binaryClosureWalker = binaryClosureWalker ?? throw new ArgumentNullException(nameof(binaryClosureWalker));
        _artifactPlanner = artifactPlanner ?? throw new ArgumentNullException(nameof(artifactPlanner));
        _filesystemCopier = filesystemCopier ?? throw new ArgumentNullException(nameof(filesystemCopier));
        _manifestConfig = manifestConfig ?? throw new ArgumentNullException(nameof(manifestConfig));
    }

    public override async Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var outputBase = context.Paths.HarvestOutput;
        var librariesToProcess = context.Vcpkg.Libraries;

        context.Log.Verbose("Cleaning harvest output directory: {0}", outputBase);
        context.CleanDirectory(outputBase);

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

            AnsiConsole.Write(new Rule($"[yellow]Harvest: {manifest.Name}[/]").RuleStyle("grey"));

            try
            {
                var closureResult = await _binaryClosureWalker.BuildClosureAsync(manifest);

                if (closureResult.IsError())
                {
                    closureResult.LogError(context.Log, manifest);
                    allSucceeded = false;
                    continue;
                }

                var closure = closureResult.Closure;

                var plan = await _artifactPlanner.CreatePlanAsync(manifest, closure, outputBase);

                if (plan.IsError())
                {
                    context.Log.Error("Plan creation failed for '{0}'.", manifest.Name);
                    allSucceeded = false;
                    continue;
                }

                var artifacts = plan.ArtifactPlan.Artifacts;

                var copierResult = await _filesystemCopier.CopyAsync(artifacts);

                if (copierResult.IsError)
                {
                    context.Log.Error("Artifact copy failed for '{0}'.", manifest.Name);
                    allSucceeded = false;
                    continue;
                }

                var harvestReport = new HarvestReport(closure.PrimaryBinary, artifacts.ToImmutableHashSet(), closure.Packages.ToImmutableHashSet());
                DisplayHarvestReportSummary(harvestReport, manifest.Name);

                AnsiConsole.Write(new Rule($"[yellow]Finished Harvest: {manifest.Name}[/]").RuleStyle("grey"));
            }
            catch (Exception ex)
            {
                context.Log.Error("Harvest failed for '{0}'. Exception Message: {1}", manifest.Name, ex.Message);
                allSucceeded = false;
            }
        }

        if (!allSucceeded)
        {
            context.Log.Verbose("Cleaning harvest output directory: {0}", outputBase);
            context.CleanDirectory(outputBase);

            throw new CakeException("One or more libraries failed to harvest. Check logs for details.");
        }

        AnsiConsole.Write(new Rule("[green]Harvest completed successfully[/]").RuleStyle("grey"));
    }

    // -------------------------------------------------------------------------

    private static void DisplayHarvestReportSummary(HarvestReport report, string libraryName)
    {
        // Key-value grid -------------------------------------------------------
        var grid = new Grid()
            .AddColumn()
            .AddColumn();
        grid.AddRow("[bold]Root Binary[/]", $"[white]{report.RootBinary.GetFilename().FullPath}[/]");
        grid.AddRow("[bold]Total Artifacts[/]", $"[white]{report.Artifacts.Count}[/]");
        grid.AddRow("[bold]Vcpkg Packages[/]", $"[white]{report.ProcessedPackageNames.Count}[/]");

        var packageNames = string.Join(", ", report.ProcessedPackageNames.Order(StringComparer.Ordinal));
        grid.AddRow("[bold]Packages List[/]", $"[teal]{packageNames}[/]");

        var infoPanel = new Panel(grid)
            .Header($"[bold yellow]{libraryName} – Summary[/]", Justify.Left)
            .BorderColor(Color.Grey);

        AnsiConsole.Write(infoPanel);

        const int maxRows = 40;
        var artTable = new Table()
            .RoundedBorder()
            .BorderColor(Color.Grey)
            .AddColumn("[bold]Origin[/]")
            .AddColumn("[bold]Package[/]")
            .AddColumn("[bold]File[/]");

        var artifacts = report.Artifacts.OrderBy(a => a.Origin).ThenBy(a => a.FileName, StringComparer.Ordinal).ToList();
        foreach (var art in artifacts.Take(maxRows))
        {
            var originColour = art.Origin switch
            {
                ArtifactOrigin.Primary => "lime",
                ArtifactOrigin.Runtime => "deepskyblue1",
                ArtifactOrigin.Metadata => "orange1",
                ArtifactOrigin.License => "grey54",
                _ => "white",
            };

            artTable.AddRow(
                $"[{originColour}]{art.Origin}[/]",
                $"[white]{art.PackageName}[/]",
                $"[grey]{art.FileName}[/]");
        }

        if (artifacts.Count > maxRows)
        {
            artTable.AddEmptyRow();
            artTable.AddRow(
                "[italic grey]…[/]",
                string.Empty,
                $"[italic grey]{artifacts.Count - maxRows} more artifact(s) omitted[/]");
        }

        var tablePanel = new Panel(artTable)
            .Header($"[bold yellow]{libraryName} – Artifacts[/]", Justify.Left)
            .BorderColor(Color.Grey);

        AnsiConsole.Write(tablePanel);
    }
}
