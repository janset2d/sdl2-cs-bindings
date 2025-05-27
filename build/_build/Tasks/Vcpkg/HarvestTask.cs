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
        var specifiedLibraries = context.Vcpkg.Libraries;
        var allManifestLibraries = _manifestConfig.LibraryManifests.ToList();

        context.Log.Verbose("Cleaning harvest output directory: {0}", outputBase);
        context.CleanDirectory(outputBase);

        List<LibraryManifest> librariesToHarvest;

        if (specifiedLibraries.Any())
        {
            context.Log.Information("Processing specified libraries for harvest: {0}", string.Join(", ", specifiedLibraries));
            librariesToHarvest = [];
            foreach (var specLibName in specifiedLibraries)
            {
                var manifest = allManifestLibraries.SingleOrDefault(m => string.Equals(m.Name, specLibName, StringComparison.OrdinalIgnoreCase));
                if (manifest == null)
                {
                    throw new CakeException($"Specified library '{specLibName}' for harvest not found in manifest.");
                }
                librariesToHarvest.Add(manifest);
            }
        }
        else
        {
            context.Log.Information("No specific libraries specified for harvest via --library. Processing all libraries from manifest.");
            librariesToHarvest = allManifestLibraries;
        }

        if (librariesToHarvest.Count == 0)
        {
            context.Log.Warning("No libraries found to harvest (either specified or in manifest).");
            return;
        }

        foreach (var manifest in librariesToHarvest)
        {
            AnsiConsole.Write(new Rule($"[yellow]Harvest: {manifest.Name}[/]").RuleStyle("grey"));

            var closureResult = await _binaryClosureWalker.BuildClosureAsync(manifest);
            closureResult.ThrowIfError(e => LogAndThrow("Binary closure", e, context.Log, manifest.Name));

            var plannerResult = await _artifactPlanner.CreatePlanAsync(manifest, closureResult.Closure, outputBase);
            plannerResult.ThrowIfError(e => LogAndThrow("Artifact planning", e, context.Log, manifest.Name));

            var copierResult = await _artifactDeployer.DeployArtifactsAsync(plannerResult.DeploymentPlan);
            copierResult.ThrowIfError(e => LogAndThrow("Artifact copying", e, context.Log, manifest.Name));

            DisplayHarvestReportSummary(plannerResult.DeploymentPlan.Statistics);
            AnsiConsole.Write(new Rule($"[yellow]Finished Harvest: {manifest.Name}[/]").RuleStyle("grey"));
        }

        AnsiConsole.Write(new Rule("[green]Harvest completed successfully[/]").RuleStyle("grey"));
    }

    private static void DisplayHarvestReportSummary(DeploymentStatistics stats)
    {
        var deployedPackagesText = stats.DeployedPackages.Any()
            ? string.Join(", ", stats.DeployedPackages.Order(StringComparer.Ordinal))
            : "None";

        var filteredPackagesText = stats.FilteredPackages.Any()
            ? string.Join(", ", stats.FilteredPackages.Order(StringComparer.Ordinal))
            : "None";

        var strategyText = stats.DeploymentStrategy switch
        {
            DeploymentStrategy.DirectCopy => "Direct copy: All files → filesystem",
            DeploymentStrategy.Archive => "Mixed: Binaries → archive, licenses → filesystem",
            _ => "Unknown",
        };

        var grid = new Grid()
            .AddColumn()
            .AddColumn();

        grid.AddRow("[bold]Library[/]", $"[white]{stats.LibraryName}[/]");
        grid.AddRow("[bold]Deployment Strategy[/]", $"[cyan]{strategyText}[/]");
        grid.AddRow("[bold]Primary Files[/]", $"[lime]{stats.PrimaryFiles.Count}[/]");
        grid.AddRow("[bold]Runtime Dependencies[/]", $"[deepskyblue1]{stats.RuntimeFiles.Count}[/]");
        grid.AddRow("[bold]License Files[/]", $"[grey54]{stats.LicenseFiles.Count}[/]");
        grid.AddRow("[bold]Deployed Packages[/]", $"[white]{stats.DeployedPackages.Count}[/]");

        if (stats.FilteredPackages.Any())
        {
            grid.AddRow("[bold]Filtered Packages[/]", $"[yellow]{stats.FilteredPackages.Count}[/] (excluded from deployment)");
        }

        var infoPanel = new Panel(grid)
            .Header($"[bold yellow]{stats.LibraryName} – Deployment Summary[/]", Justify.Left)
            .BorderColor(Color.Grey);

        AnsiConsole.Write(infoPanel);

        // Show primary files detail
        if (stats.PrimaryFiles.Any())
        {
            var primaryTable = new Table()
                .RoundedBorder()
                .BorderColor(Color.Green)
                .AddColumn("[bold]Primary Files[/]")
                .AddColumn("[bold]Location[/]");

            foreach (var fileInfo in stats.PrimaryFiles.OrderBy(f => f.FilePath.GetFilename().FullPath, StringComparer.Ordinal))
            {
                var locationText = fileInfo.DeploymentLocation switch
                {
                    DeploymentLocation.FileSystem => "[white]Filesystem[/]",
                    DeploymentLocation.Archive => "[cyan]Archive[/]",
                    _ => "[grey]Unknown[/]",
                };

                primaryTable.AddRow($"[lime]{fileInfo.FilePath.GetFilename().FullPath}[/]", locationText);
            }

            AnsiConsole.Write(primaryTable);
        }

        // Show package breakdown
        var packageTable = new Table()
            .RoundedBorder()
            .BorderColor(Color.Blue)
            .AddColumn("[bold]Package Type[/]")
            .AddColumn("[bold]Packages[/]");

        packageTable.AddRow("[deepskyblue1]Deployed[/]", $"[white]{deployedPackagesText}[/]");

        if (stats.FilteredPackages.Any())
        {
            packageTable.AddRow("[yellow]Filtered[/]", $"[grey]{filteredPackagesText}[/]");
        }

        AnsiConsole.Write(packageTable);

        // Show detailed file listing if there are runtime dependencies or license files
        if (stats.RuntimeFiles.Any() || stats.PrimaryFiles.Any() || stats.LicenseFiles.Any())
        {
            var detailTable = new Table()
                .RoundedBorder()
                .BorderColor(Color.Grey)
                .AddColumn("[bold]Type[/]")
                .AddColumn("[bold]File[/]")
                .AddColumn("[bold]Package[/]")
                .AddColumn("[bold]Location[/]");

            //Add primary files
            foreach (var fileInfo in stats.PrimaryFiles.OrderBy(f => f.PackageName, StringComparer.Ordinal).ThenBy(f => f.FilePath.GetFilename().FullPath, StringComparer.Ordinal))
            {
                var locationText = fileInfo.DeploymentLocation switch
                {
                    DeploymentLocation.FileSystem => "[white]Filesystem[/]",
                    DeploymentLocation.Archive => "[cyan]Archive[/]",
                    _ => "[grey]Unknown[/]",
                };

                detailTable.AddRow(
                    "[lime]Primary[/]",
                    $"[white]{fileInfo.FilePath.GetFilename().FullPath}[/]",
                    $"[grey]{fileInfo.PackageName}[/]",
                    locationText);
            }

            // Add runtime files
            foreach (var fileInfo in stats.RuntimeFiles.OrderBy(f => f.PackageName, StringComparer.Ordinal).ThenBy(f => f.FilePath.GetFilename().FullPath, StringComparer.Ordinal))
            {
                var locationText = fileInfo.DeploymentLocation switch
                {
                    DeploymentLocation.FileSystem => "[white]Filesystem[/]",
                    DeploymentLocation.Archive => "[cyan]Archive[/]",
                    _ => "[grey]Unknown[/]",
                };

                detailTable.AddRow(
                    "[deepskyblue1]Runtime[/]",
                    $"[white]{fileInfo.FilePath.GetFilename().FullPath}[/]",
                    $"[grey]{fileInfo.PackageName}[/]",
                    locationText);
            }

            // Add license files
            foreach (var fileInfo in stats.LicenseFiles.OrderBy(f => f.PackageName, StringComparer.Ordinal).ThenBy(f => f.FilePath.GetFilename().FullPath, StringComparer.Ordinal))
            {
                var locationText = fileInfo.DeploymentLocation switch
                {
                    DeploymentLocation.FileSystem => "[white]Filesystem[/]",
                    DeploymentLocation.Archive => "[cyan]Archive[/]",
                    _ => "[grey]Unknown[/]",
                };

                detailTable.AddRow(
                    "[grey54]License[/]",
                    $"[white]{fileInfo.FilePath.GetFilename().FullPath}[/]",
                    $"[grey]{fileInfo.PackageName}[/]",
                    locationText);
            }

            var detailPanel = new Panel(detailTable)
                .Header($"[bold yellow]{stats.LibraryName} – Detailed File List[/]", Justify.Left)
                .BorderColor(Color.Grey);

            AnsiConsole.Write(detailPanel);
        }
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
