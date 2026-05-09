using Build.Results;
using Build.Targets.Harvest.Models;
using Cake.Core.Diagnostics;
using Spectre.Console;

namespace Build.Targets.Harvest.Reporting;

/// <summary>
/// Consolidates Harvest's user-facing visual + log surface behind a single named seam. Owns
/// every <c>IAnsiConsole</c> render and structured <c>ICakeLog</c> entry that Harvest emits.
/// Sealed concrete; cohort consistency with <c>PreflightReporter</c> + <c>PackageReporter</c>.
/// </summary>
public sealed class HarvestReporter(IAnsiConsole console, ICakeLog log)
{
    private readonly IAnsiConsole _console = console ?? throw new ArgumentNullException(nameof(console));
    private readonly ICakeLog _log = log ?? throw new ArgumentNullException(nameof(log));

    public void LogStarting(IReadOnlyCollection<string> libraryNames)
    {
        ArgumentNullException.ThrowIfNull(libraryNames);

        if (libraryNames.Count == 0)
        {
            _log.Information("No specific libraries specified for harvest. Processing all libraries from manifest.");
            return;
        }

        _log.Information("Processing specified libraries for harvest: {0}", string.Join(", ", libraryNames));
    }

    public void LogCompleted()
    {
        _console.Write(new Rule("[green]Harvest completed successfully[/]"));
    }

    public void StartLibrary(string libraryName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryName);
        _console.Write(new Rule($"[yellow]Harvest: {Markup.Escape(libraryName)}[/]"));
    }

    public void FinishLibrary(string libraryName, DeploymentStatistics statistics)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryName);
        ArgumentNullException.ThrowIfNull(statistics);

        WriteSummaryPanel(statistics);
        WritePrimaryFilesTable(statistics);
        WritePackageBreakdown(statistics);
        WriteDetailPanel(statistics);
        _console.Write(new Rule($"[green]Finished Harvest: {Markup.Escape(libraryName)}[/]"));
    }

    public void CancelLibrary(string libraryName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryName);
        _log.Warning("Harvest canceled for '{0}'.", libraryName);
        _console.Write(new Rule($"[yellow]Canceled Harvest: {Markup.Escape(libraryName)}[/]"));
    }

    public void ReportPhaseFailure(string libraryName, string phase, string errorMessage, Exception? exception = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryName);
        ArgumentException.ThrowIfNullOrWhiteSpace(phase);
        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);

        _log.Error("{0} failed for '{1}': {2}", phase, libraryName, errorMessage);
        if (exception is not null)
        {
            _log.Verbose("Details: {0}", exception);
        }

        _console.Write(new Rule($"[red]Failed Harvest: {Markup.Escape(libraryName)}[/]"));
    }

    public void ReportLeakReport(string libraryName, ValidationReport report)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryName);
        ArgumentNullException.ThrowIfNull(report);

        if (!report.IsValid)
        {
            _log.Error("Hybrid-static leak validation failed for '{0}': {1} violation(s) detected.", libraryName, report.Errors.Count);
            foreach (var error in report.Errors)
            {
                _log.Error("  - {0}", error.Message);
            }
        }

        if (report.HasWarnings)
        {
            _log.Warning(
                "Hybrid-static leak validation produced {0} warning(s) for '{1}' (non-blocking).",
                report.Warnings.Count,
                libraryName);
            foreach (var warning in report.Warnings)
            {
                _log.Warning("  - {0}", warning.Message);
            }
        }
    }

    private void WriteSummaryPanel(DeploymentStatistics stats)
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

        if (stats.FilteredPackages.Count > 0)
        {
            grid.AddRow("[bold]Filtered Packages[/]", $"[yellow]{stats.FilteredPackages.Count}[/] (excluded from deployment)");
        }

        var infoPanel = new Panel(grid)
            .Header($"[bold yellow]{Markup.Escape(stats.LibraryName)} - Deployment Summary[/]", Justify.Left)
            .BorderColor(Color.Grey);

        _console.Write(infoPanel);
    }

    private void WritePrimaryFilesTable(DeploymentStatistics stats)
    {
        if (stats.PrimaryFiles.Count == 0)
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

        _console.Write(primaryTable);
    }

    private void WritePackageBreakdown(DeploymentStatistics stats)
    {
        var packageTable = new Table()
            .RoundedBorder()
            .BorderColor(Color.Blue)
            .AddColumn("[bold]Package Type[/]")
            .AddColumn("[bold]Packages[/]");

        packageTable.AddRow("[deepskyblue1]Deployed[/]", $"[white]{FormatPackages(stats.DeployedPackages)}[/]");

        if (stats.FilteredPackages.Count > 0)
        {
            packageTable.AddRow("[yellow]Filtered[/]", $"[grey]{FormatPackages(stats.FilteredPackages)}[/]");
        }

        _console.Write(packageTable);
    }

    private void WriteDetailPanel(DeploymentStatistics stats)
    {
        if (stats.RuntimeFiles.Count == 0 && stats.PrimaryFiles.Count == 0 && stats.LicenseFiles.Count == 0)
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
            .Header($"[bold yellow]{Markup.Escape(stats.LibraryName)} - Detailed File List[/]", Justify.Left)
            .BorderColor(Color.Grey);

        _console.Write(detailPanel);
    }

    private static void AddDetailRows(Table detailTable, string typeLabel, IReadOnlyList<FileDeploymentInfo> files)
    {
        foreach (var fileInfo in files.OrderBy(f => f.PackageName, StringComparer.Ordinal).ThenBy(f => f.FilePath.GetFilename().FullPath, StringComparer.Ordinal))
        {
            detailTable.AddRow(
                typeLabel,
                $"[white]{fileInfo.FilePath.GetFilename().FullPath}[/]",
                $"[grey]{fileInfo.PackageName}[/]",
                ToLocationText(fileInfo.DeploymentLocation));
        }
    }

    private static string DescribeDeploymentStrategy(DeploymentStrategy strategy) => strategy switch
    {
        DeploymentStrategy.DirectCopy => "Direct copy: All files -> filesystem",
        DeploymentStrategy.Archive => "Mixed: Binaries -> archive, licenses -> filesystem",
        _ => "Unknown",
    };

    private static string FormatPackages(IEnumerable<string> packages)
    {
        var joined = string.Join(", ", packages.Order(StringComparer.Ordinal));
        return string.IsNullOrEmpty(joined) ? "None" : joined;
    }

    private static string ToLocationText(DeploymentLocation deploymentLocation) => deploymentLocation switch
    {
        DeploymentLocation.FileSystem => "[white]Filesystem[/]",
        DeploymentLocation.Archive => "[cyan]Archive[/]",
        _ => "[grey]Unknown[/]",
    };
}
