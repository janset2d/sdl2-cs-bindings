using Build.Context;
using Build.Context.Models;
using Build.Modules.Contracts;
using Build.Modules.Harvesting.Models;
using Build.Modules.Harvesting.Results;
using Build.Modules.Results;
using Build.Modules.Strategy.Results;
using Build.Tasks.Common;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Cake.Frosting;
using Spectre.Console;
using System.Text.Json;

namespace Build.Tasks.Harvest;

[TaskName("Harvest")]
[IsDependentOn(typeof(InfoTask))]
public sealed class HarvestTask(
    IBinaryClosureWalker binaryClosureWalker,
    IArtifactPlanner artifactPlanner,
    IArtifactDeployer artifactDeployer,
    IDependencyPolicyValidator dependencyPolicyValidator,
    IRuntimeProfile runtimeProfile,
    ManifestConfig manifestConfig) : AsyncFrostingTask<BuildContext>
{
    private readonly IBinaryClosureWalker _binaryClosureWalker = binaryClosureWalker ?? throw new ArgumentNullException(nameof(binaryClosureWalker));
    private readonly IArtifactPlanner _artifactPlanner = artifactPlanner ?? throw new ArgumentNullException(nameof(artifactPlanner));
    private readonly IArtifactDeployer _artifactDeployer = artifactDeployer ?? throw new ArgumentNullException(nameof(artifactDeployer));
    private readonly IDependencyPolicyValidator _dependencyPolicyValidator = dependencyPolicyValidator ?? throw new ArgumentNullException(nameof(dependencyPolicyValidator));
    private readonly ManifestConfig _manifestConfig = manifestConfig ?? throw new ArgumentNullException(nameof(manifestConfig));
    private readonly IRuntimeProfile _runtimeProfile = runtimeProfile ?? throw new ArgumentNullException(nameof(runtimeProfile));

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public override async Task RunAsync(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var outputBase = context.Paths.HarvestOutput;

        context.Log.Verbose("Cleaning harvest output directory: {0}", outputBase);
        context.CleanDirectory(outputBase);

        var librariesToHarvest = ResolveLibrariesToHarvest(context);

        if (librariesToHarvest.Count == 0)
        {
            context.Log.Warning("No libraries found to harvest (either specified or in manifest).");
            return;
        }

        foreach (var manifest in librariesToHarvest)
        {
            await ProcessLibraryAsync(context, manifest, outputBase);
        }

        AnsiConsole.Write(new Rule("[green]Harvest completed successfully[/]").RuleStyle("grey"));
    }

    private List<LibraryManifest> ResolveLibrariesToHarvest(BuildContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var specifiedLibraries = context.Vcpkg.Libraries;
        var allManifestLibraries = _manifestConfig.LibraryManifests.ToList();

        if (!specifiedLibraries.Any())
        {
            context.Log.Information("No specific libraries specified for harvest via --library. Processing all libraries from manifest.");
            return allManifestLibraries;
        }

        context.Log.Information("Processing specified libraries for harvest: {0}", string.Join(", ", specifiedLibraries));

        var librariesToHarvest = new List<LibraryManifest>(specifiedLibraries.Count);
        foreach (var specLibName in specifiedLibraries)
        {
            var manifest = allManifestLibraries.SingleOrDefault(m => string.Equals(m.Name, specLibName, StringComparison.OrdinalIgnoreCase))
                ?? throw new CakeException($"Specified library '{specLibName}' for harvest not found in manifest.");

            librariesToHarvest.Add(manifest);
        }

        return librariesToHarvest;
    }

    private async Task ProcessLibraryAsync(BuildContext context, LibraryManifest manifest, DirectoryPath outputBase)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(outputBase);

        AnsiConsole.Write(new Rule($"[yellow]Harvest: {manifest.Name}[/]").RuleStyle("grey"));

        try
        {
            var statistics = await ExecuteHarvestPipelineAsync(context, manifest, outputBase);
            DisplayHarvestReportSummary(statistics);
            AnsiConsole.Write(new Rule($"[green]Finished Harvest: {manifest.Name}[/]").RuleStyle("grey"));
        }
        catch (OperationCanceledException)
        {
            context.Log.Warning("Harvest canceled for '{0}'.", manifest.Name);
            AnsiConsole.Write(new Rule($"[yellow]Canceled Harvest: {manifest.Name}[/]").RuleStyle("grey"));
            throw;
        }
        catch (CakeException)
        {
            await HandleKnownHarvestFailureAsync(context, manifest, outputBase, $"Harvest failed for {manifest.Name}");
            throw;
        }
        catch (Exception ex) when (IsOperationalHarvestException(ex))
        {
            await HandleOperationalHarvestFailureAsync(context, manifest, outputBase, ex);
            throw;
        }
    }

    private async Task<DeploymentStatistics> ExecuteHarvestPipelineAsync(BuildContext context, LibraryManifest manifest, DirectoryPath outputBase)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(outputBase);

        var closureResult = await _binaryClosureWalker.BuildClosureAsync(manifest);
        closureResult.OnError(e => LogAndThrow("Binary closure", e, context.Log, manifest.Name));

        var validationResult = _dependencyPolicyValidator.Validate(closureResult.Closure, manifest);
        validationResult.OnError(e => LogAndThrowValidation(e, context.Log, manifest.Name));

        if (validationResult.ValidationSuccess.HasWarnings)
        {
            LogValidationWarnings(context.Log, manifest.Name, validationResult.ValidationSuccess.Warnings);
        }

        var plannerResult = await _artifactPlanner.CreatePlanAsync(manifest, closureResult.Closure, outputBase);
        plannerResult.OnError(e => LogAndThrow("Artifact planning", e, context.Log, manifest.Name));

        var copierResult = await _artifactDeployer.DeployArtifactsAsync(plannerResult.DeploymentPlan);
        copierResult.OnError(e => LogAndThrow("Artifact copying", e, context.Log, manifest.Name));

        await GenerateRidStatusFileAsync(context, manifest, plannerResult.DeploymentPlan.Statistics, outputBase);
        return plannerResult.DeploymentPlan.Statistics;
    }

    private static void DisplayHarvestReportSummary(DeploymentStatistics stats)
    {
        DisplaySummaryPanel(stats);
        DisplayPrimaryFilesTable(stats);
        DisplayPackageBreakdown(stats);
        DisplayDetailPanel(stats);
    }

    private static void DisplaySummaryPanel(DeploymentStatistics stats)
    {
        var grid = new Grid()
            .AddColumn()
            .AddColumn();

        grid.AddRow("[bold]Library[/]", $"[white]{stats.LibraryName}[/]");
        grid.AddRow("[bold]Deployment Strategy[/]", $"[cyan]{DescribeDeploymentStrategy(stats.DeploymentStrategy)}[/]");
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
    }

    private static void DisplayPrimaryFilesTable(DeploymentStatistics stats)
    {
        if (!stats.PrimaryFiles.Any())
        {
            return;
        }

        var primaryTable = new Table()
            .RoundedBorder()
            .BorderColor(Color.Green)
            .AddColumn("[bold]Primary Files[/]")
            .AddColumn("[bold]Location[/]");

        foreach (var fileInfo in stats.PrimaryFiles.OrderBy(f => f.FilePath.GetFilename().FullPath, StringComparer.Ordinal))
        {
            primaryTable.AddRow($"[lime]{fileInfo.FilePath.GetFilename().FullPath}[/]", ToLocationText(fileInfo.DeploymentLocation));
        }

        AnsiConsole.Write(primaryTable);
    }

    private static void DisplayPackageBreakdown(DeploymentStatistics stats)
    {
        var packageTable = new Table()
            .RoundedBorder()
            .BorderColor(Color.Blue)
            .AddColumn("[bold]Package Type[/]")
            .AddColumn("[bold]Packages[/]");

        packageTable.AddRow("[deepskyblue1]Deployed[/]", $"[white]{FormatPackages(stats.DeployedPackages)}[/]");

        if (stats.FilteredPackages.Any())
        {
            packageTable.AddRow("[yellow]Filtered[/]", $"[grey]{FormatPackages(stats.FilteredPackages)}[/]");
        }

        AnsiConsole.Write(packageTable);
    }

    private static void DisplayDetailPanel(DeploymentStatistics stats)
    {
        if (!stats.RuntimeFiles.Any() && !stats.PrimaryFiles.Any() && !stats.LicenseFiles.Any())
        {
            return;
        }

        var detailTable = new Table()
            .RoundedBorder()
            .BorderColor(Color.Grey)
            .AddColumn("[bold]Type[/]")
            .AddColumn("[bold]File[/]")
            .AddColumn("[bold]Package[/]")
            .AddColumn("[bold]Location[/]");

        AddDetailRows(detailTable, "[lime]Primary[/]", stats.PrimaryFiles);
        AddDetailRows(detailTable, "[deepskyblue1]Runtime[/]", stats.RuntimeFiles);
        AddDetailRows(detailTable, "[grey54]License[/]", stats.LicenseFiles);

        var detailPanel = new Panel(detailTable)
            .Header($"[bold yellow]{stats.LibraryName} – Detailed File List[/]", Justify.Left)
            .BorderColor(Color.Grey);

        AnsiConsole.Write(detailPanel);
    }

    private static void AddDetailRows(Table detailTable, string typeLabel, IReadOnlyList<FileDeploymentInfo> files)
    {
        ArgumentNullException.ThrowIfNull(detailTable);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentException.ThrowIfNullOrEmpty(typeLabel);

        foreach (var fileInfo in files.OrderBy(f => f.PackageName, StringComparer.Ordinal).ThenBy(f => f.FilePath.GetFilename().FullPath, StringComparer.Ordinal))
        {
            detailTable.AddRow(
                typeLabel,
                $"[white]{fileInfo.FilePath.GetFilename().FullPath}[/]",
                $"[grey]{fileInfo.PackageName}[/]",
                ToLocationText(fileInfo.DeploymentLocation));
        }
    }

    private static string DescribeDeploymentStrategy(DeploymentStrategy strategy)
    {
        return strategy switch
        {
            DeploymentStrategy.DirectCopy => "Direct copy: All files → filesystem",
            DeploymentStrategy.Archive => "Mixed: Binaries → archive, licenses → filesystem",
            _ => "Unknown",
        };
    }

    private static string FormatPackages(IEnumerable<string> packages)
    {
        ArgumentNullException.ThrowIfNull(packages);

        var orderedPackages = packages.Order(StringComparer.Ordinal).ToList();
        return orderedPackages.Count == 0 ? "None" : string.Join(", ", orderedPackages);
    }

    private static string ToLocationText(DeploymentLocation deploymentLocation)
    {
        return deploymentLocation switch
        {
            DeploymentLocation.FileSystem => "[white]Filesystem[/]",
            DeploymentLocation.Archive => "[cyan]Archive[/]",
            _ => "[grey]Unknown[/]",
        };
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

    private static void LogAndThrowValidation(ValidationError error, ICakeLog log, string libraryName)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(log);
        ArgumentException.ThrowIfNullOrEmpty(libraryName);

        log.Error("Dependency policy validation failed for '{0}': {1}", libraryName, error.Message);

        foreach (var violation in error.Violations)
        {
            log.Error(
                "  - {0} (owner: {1}, origin: {2})",
                violation.Path.GetFilename().FullPath,
                violation.OwnerPackage,
                violation.OriginPackage);
        }

        throw new CakeException(
            $"Dependency policy validation failed for '{libraryName}'. " +
            $"Use –verbosity=diagnostic for details. Error: {error.Message}");
    }

    private static void LogValidationWarnings(ICakeLog log, string libraryName, IReadOnlyList<BinaryNode> warnings)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentException.ThrowIfNullOrEmpty(libraryName);
        ArgumentNullException.ThrowIfNull(warnings);

        log.Warning(
            "Dependency policy validation produced {0} warning(s) for '{1}' (non-blocking).",
            warnings.Count,
            libraryName);

        foreach (var warning in warnings)
        {
            log.Warning(
                "  - {0} (owner: {1}, origin: {2})",
                warning.Path.GetFilename().FullPath,
                warning.OwnerPackage,
                warning.OriginPackage);
        }
    }

    private async Task HandleKnownHarvestFailureAsync(BuildContext context, LibraryManifest manifest, DirectoryPath outputBase, string errorMessage)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(outputBase);
        ArgumentException.ThrowIfNullOrEmpty(errorMessage);

        await GenerateErrorRidStatusFileAsync(context, manifest, outputBase, errorMessage);
        AnsiConsole.Write(new Rule($"[red]Failed Harvest: {manifest.Name}[/]").RuleStyle("grey"));
    }

    private async Task HandleOperationalHarvestFailureAsync(BuildContext context, LibraryManifest manifest, DirectoryPath outputBase, Exception ex)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(outputBase);
        ArgumentNullException.ThrowIfNull(ex);

        context.Log.Error("Unexpected operational error during harvest of {0}: {1}", manifest.Name, ex.Message);
        context.Log.Verbose("Harvest error details: {0}", ex);

        await GenerateErrorRidStatusFileAsync(context, manifest, outputBase, ex.Message);
        AnsiConsole.Write(new Rule($"[red]Failed Harvest: {manifest.Name}[/]").RuleStyle("grey"));
    }

    /// <summary>
    /// Generates a RID-specific status file for later consolidation into the harvest manifest.
    /// This allows CI matrix jobs to run per library+RID while still generating a complete
    /// library-wide harvest manifest in a subsequent consolidation step.
    /// </summary>
    private async Task GenerateRidStatusFileAsync(BuildContext context, LibraryManifest manifest, DeploymentStatistics statistics, DirectoryPath outputBase)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(statistics);
        ArgumentNullException.ThrowIfNull(outputBase);

        try
        {
            var ridStatusDir = outputBase.Combine(manifest.Name).Combine("rid-status");
            context.EnsureDirectoryExists(ridStatusDir);

            var ridStatus = new RidHarvestStatus
            {
                LibraryName = manifest.Name,
                Rid = _runtimeProfile.Rid,
                Triplet = _runtimeProfile.Triplet,
                Success = true,
                ErrorMessage = null,
                Timestamp = DateTimeOffset.UtcNow,
                Statistics = new HarvestStatistics
                {
                    PrimaryFilesCount = statistics.PrimaryFiles.Count,
                    RuntimeFilesCount = statistics.RuntimeFiles.Count,
                    LicenseFilesCount = statistics.LicenseFiles.Count,
                    DeployedPackagesCount = statistics.DeployedPackages.Count,
                    FilteredPackagesCount = statistics.FilteredPackages.Count,
                    DeploymentStrategy = statistics.DeploymentStrategy.ToString()
                },
            };

            var statusFileName = $"{_runtimeProfile.Rid}.json";
            var statusFilePath = ridStatusDir.CombineWithFilePath(statusFileName);

            var jsonContent = JsonSerializer.Serialize(ridStatus, JsonOptions);
            await context.WriteAllTextAsync(statusFilePath, jsonContent);

            context.Log.Information("Generated RID status file: {0}", statusFilePath);
        }
        catch (Exception ex) when (IsOperationalHarvestException(ex))
        {
            LogRidStatusGenerationFailure(context, manifest, ex);

            // Generate an error status file so consolidation knows this RID failed
            await GenerateErrorRidStatusFileAsync(context, manifest, outputBase, ex.Message);
        }
    }

    /// <summary>
    /// Generates an error RID status file when harvest fails for a specific RID.
    /// </summary>
    private async Task GenerateErrorRidStatusFileAsync(BuildContext context, LibraryManifest manifest, DirectoryPath outputBase, string errorMessage)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(outputBase);
        ArgumentException.ThrowIfNullOrEmpty(errorMessage);

        try
        {
            var ridStatusDir = outputBase.Combine(manifest.Name).Combine("rid-status");
            context.EnsureDirectoryExists(ridStatusDir);

            var ridStatus = new RidHarvestStatus
            {
                LibraryName = manifest.Name,
                Rid = _runtimeProfile.Rid,
                Triplet = _runtimeProfile.Triplet,
                Success = false,
                ErrorMessage = errorMessage,
                Timestamp = DateTimeOffset.UtcNow,
                Statistics = null,
            };

            var statusFileName = $"{_runtimeProfile.Rid}.json";
            var statusFilePath = ridStatusDir.CombineWithFilePath(statusFileName);

            var jsonContent = JsonSerializer.Serialize(ridStatus, JsonOptions);
            await context.WriteAllTextAsync(statusFilePath, jsonContent);

            context.Log.Information("Generated error RID status file: {0}", statusFilePath);
        }
        catch (Exception ex) when (IsOperationalHarvestException(ex))
        {
            LogErrorRidStatusGenerationFailure(context, manifest, ex);
        }
    }

    private static bool IsOperationalHarvestException(Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);

        return ex is IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException or JsonException;
    }

    private void LogRidStatusGenerationFailure(BuildContext context, LibraryManifest manifest, Exception ex)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(ex);

        context.Log.Warning("Failed to generate RID status file for {0}/{1}: {2}", manifest.Name, _runtimeProfile.Rid, ex.Message);
        context.Log.Verbose("RID status file generation error details: {0}", ex);
    }

    private void LogErrorRidStatusGenerationFailure(BuildContext context, LibraryManifest manifest, Exception ex)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(ex);

        context.Log.Error("Failed to generate error RID status file for {0}/{1}: {2}", manifest.Name, _runtimeProfile.Rid, ex.Message);
        context.Log.Verbose("Error RID status file generation error details: {0}", ex);
    }
}
