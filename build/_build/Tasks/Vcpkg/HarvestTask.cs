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
public sealed class HarvestTask : AsyncFrostingTask<BuildContext>
{
    private readonly IBinaryClosureWalker _binaryClosureWalker;
    private readonly IArtifactPlanner _artifactPlanner;
    private readonly IArtifactDeployer _artifactDeployer;
    private readonly ManifestConfig _manifestConfig;

    public HarvestTask(IBinaryClosureWalker binaryClosureWalker, IArtifactPlanner artifactPlanner, IArtifactDeployer artifactDeployer, ManifestConfig manifestConfig)
    {
        _binaryClosureWalker = binaryClosureWalker ?? throw new ArgumentNullException(nameof(binaryClosureWalker));
        _artifactPlanner = artifactPlanner ?? throw new ArgumentNullException(nameof(artifactPlanner));
        _artifactDeployer = artifactDeployer ?? throw new ArgumentNullException(nameof(artifactDeployer));
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

        foreach (var library in librariesToProcess)
        {
            var manifest = _manifestConfig.LibraryManifests.SingleOrDefault(m => string.Equals(m.Name, library, StringComparison.OrdinalIgnoreCase))
                           ?? throw new CakeException($"Library '{library}' not found in manifest.");

            AnsiConsole.Write(new Rule($"[yellow]Harvest: {manifest.Name}[/]").RuleStyle("grey"));

            var closureResult = await _binaryClosureWalker.BuildClosureAsync(manifest);
            closureResult.ThrowIfError(e => LogAndThrow("Binary closure", e, context.Log, manifest.Name));

            var plannerResult = await _artifactPlanner.CreatePlanAsync(manifest, closureResult.Closure, outputBase);
            plannerResult.ThrowIfError(e => LogAndThrow("Artifact planning", e, context.Log, manifest.Name));

            var copierResult = await _artifactDeployer.DeployArtifactsAsync(plannerResult.ArtifactPlan);
            copierResult.ThrowIfError(e => LogAndThrow("Artifact copying", e, context.Log, manifest.Name));

            DisplayHarvestReportSummary(closureResult.Closure, plannerResult.ArtifactPlan, manifest.Name);
            AnsiConsole.Write(new Rule($"[yellow]Finished Harvest: {manifest.Name}[/]").RuleStyle("grey"));
        }

        AnsiConsole.Write(new Rule("[green]Harvest completed successfully[/]").RuleStyle("grey"));
    }

    private static void DisplayHarvestReportSummary(BinaryClosure binaryClosure, DeploymentPlan deploymentPlan, string libraryName)
    {
        var primaryBinary = binaryClosure.PrimaryBinary;
        var packagesInClosure = binaryClosure.Packages;

        var reportableArtifacts = new List<(string FileName, string PackageName, ArtifactOrigin Origin)>();
        foreach (var action in deploymentPlan.Actions)
        {
            switch (action)
            {
                case FileCopyAction copyAction:
                    reportableArtifacts.Add((copyAction.SourcePath.GetFilename().FullPath, copyAction.PackageName, copyAction.Origin));
                    break;
                case ArchiveCreationAction archiveAction:
                    reportableArtifacts.Add((archiveAction.ArchiveName, libraryName, ArtifactOrigin.Primary));
                    reportableArtifacts.AddRange(archiveAction.ItemsToArchive.Select(item => (item.SourcePath.GetFilename().FullPath, item.PackageName, item.Origin)));
                    break;
            }
        }

        var grid = new Grid()
            .AddColumn()
            .AddColumn();
        grid.AddRow("[bold]Root Binary[/]", $"[white]{primaryBinary.GetFilename().FullPath}[/]");
        grid.AddRow("[bold]Total Actions[/]", $"[white]{deploymentPlan.Actions.Count}[/]");
        grid.AddRow("[bold]Total Reported Artifacts[/]", $"[white]{reportableArtifacts.Count}[/]");
        grid.AddRow("[bold]Vcpkg Packages in Closure[/]", $"[white]{packagesInClosure.Count}[/]");

        var packageNamesText = packagesInClosure.Any() ? string.Join(", ", packagesInClosure.Order(StringComparer.Ordinal)) : "N/A";
        grid.AddRow("[bold]Packages List[/]", $"[teal]{packageNamesText}[/]");

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
            .AddColumn("[bold]File/Item[/]");

        var orderedReportableArtifacts = reportableArtifacts
            .OrderBy(a => a.Origin)
            .ThenBy(a => a.FileName, StringComparer.Ordinal)
            .ToList();

        foreach (var (fileName, packageName, origin) in orderedReportableArtifacts.Take(maxRows))
        {
            var originColour = origin switch
            {
                ArtifactOrigin.Primary => "lime",
                ArtifactOrigin.Runtime => "deepskyblue1",
                ArtifactOrigin.Metadata => "orange1",
                ArtifactOrigin.License => "grey54",
                _ => "white",
            };

            artTable.AddRow(
                $"[{originColour}]{origin}[/]",
                $"[white]{packageName}[/]",
                $"[grey]{fileName}[/]");
        }

        if (orderedReportableArtifacts.Count > maxRows)
        {
            artTable.AddEmptyRow();
            artTable.AddRow(
                "[italic grey]…[/]",
                string.Empty,
                $"[italic grey]{reportableArtifacts.Count - maxRows} more item(s) omitted[/]");
        }

        var tablePanel = new Panel(artTable)
            .Header($"[bold yellow]{libraryName} – Reported Items[/]", Justify.Left)
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

        throw new CakeException($"{phase} failed for '{libraryName}'. Use –verbosity=diagnostic for details. Error: {error.Message}");
    }
}
