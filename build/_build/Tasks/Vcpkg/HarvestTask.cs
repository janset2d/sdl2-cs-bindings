#pragma warning disable CA1031, MA0051

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
public sealed class HarvestTask(IBinaryClosureWalker binaryClosureWalker, IArtifactPlanner artifactPlanner, IFilesystemCopier filesystemCopier, ManifestConfig manifestConfig)
    : AsyncFrostingTask<BuildContext>
{
    private readonly IBinaryClosureWalker _binaryClosureWalker = binaryClosureWalker ?? throw new ArgumentNullException(nameof(binaryClosureWalker));
    private readonly IArtifactPlanner _artifactPlanner = artifactPlanner ?? throw new ArgumentNullException(nameof(artifactPlanner));
    private readonly IFilesystemCopier _filesystemCopier = filesystemCopier ?? throw new ArgumentNullException(nameof(filesystemCopier));
    private readonly ManifestConfig _manifestConfig = manifestConfig ?? throw new ArgumentNullException(nameof(manifestConfig));

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

        foreach (var library in librariesToProcess)
        {
            var manifest = _manifestConfig.LibraryManifests.SingleOrDefault(m => string.Equals(m.Name, library, StringComparison.OrdinalIgnoreCase))
                           ?? throw new CakeException($"Library '{library}' not found in manifest.");

            AnsiConsole.Write(new Rule($"[yellow]Harvest: {manifest.Name}[/]").RuleStyle("grey"));

            var closureResult = await _binaryClosureWalker.BuildClosureAsync(manifest);
            closureResult.ThrowIfError(e => LogAndThrow("Binary closure", e, context.Log, manifest.Name));

            var plannerResult = await _artifactPlanner.CreatePlanAsync(manifest, closureResult.Closure, outputBase);
            plannerResult.ThrowIfError(e => LogAndThrow("Artifact planning", e, context.Log, manifest.Name));

            var copierResult = await _filesystemCopier.CopyAsync(plannerResult.ArtifactPlan.Artifacts);
            copierResult.ThrowIfError(e => LogAndThrow("Artifact copying", e, context.Log, manifest.Name));

            DisplayHarvestReportSummary(closureResult.Closure, plannerResult.ArtifactPlan, manifest.Name);
            AnsiConsole.Write(new Rule($"[yellow]Finished Harvest: {manifest.Name}[/]").RuleStyle("grey"));
        }

        AnsiConsole.Write(new Rule("[green]Harvest completed successfully[/]").RuleStyle("grey"));
    }

    private static void DisplayHarvestReportSummary(BinaryClosure binaryClosure, ArtifactPlan artifactPlan, string libraryName)
    {
        var primaryBinary = binaryClosure.PrimaryBinary;
        var packages = binaryClosure.Packages;
        var artifacts = artifactPlan.Artifacts;

        var grid = new Grid()
            .AddColumn()
            .AddColumn();
        grid.AddRow("[bold]Root Binary[/]", $"[white]{primaryBinary.GetFilename().FullPath}[/]");
        grid.AddRow("[bold]Total Artifacts[/]", $"[white]{artifacts.Count}[/]");
        grid.AddRow("[bold]Vcpkg Packages[/]", $"[white]{packages.Count}[/]");

        var packageNames = string.Join(", ", packages.Order(StringComparer.Ordinal));
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

        var orderedArtifacts = artifacts.OrderBy(a => a.Origin).ThenBy(a => a.FileName, StringComparer.Ordinal).ToList();
        foreach (var art in orderedArtifacts.Take(maxRows))
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

        if (orderedArtifacts.Count > maxRows)
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

    private static void LogAndThrow(string phase, HarvestingError error, ICakeLog log, string libraryName)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(log);
        ArgumentException.ThrowIfNullOrEmpty(phase);
        ArgumentException.ThrowIfNullOrEmpty(libraryName);

        log.Error("{0} failed for '{1}': {2}", phase, libraryName, error.Message);

        if (error.Exception is not null)
        {
            log.Verbose("Details: {0}", error.Exception);
        }

        throw new CakeException($"{phase} failed for '{libraryName}'. Use –verbosity=diagnostic for details.");
    }
}
