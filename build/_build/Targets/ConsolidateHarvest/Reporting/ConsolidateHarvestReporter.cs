using Cake.Core.Diagnostics;
using Spectre.Console;

namespace Build.Targets.ConsolidateHarvest.Reporting;

/// <summary>
/// Consolidates ConsolidateHarvest's user-facing visual + log surface behind a single named
/// seam. Mirrors <c>HarvestReporter</c>'s shape: <see cref="IAnsiConsole"/> rules at library
/// boundaries + completion, <see cref="ICakeLog"/> for structured info/warning/error entries
/// the operator inspects in CI logs. Sealed concrete; cohort consistency with the harvest +
/// preflight reporters.
/// </summary>
public sealed class ConsolidateHarvestReporter(IAnsiConsole console, ICakeLog log)
{
    private readonly IAnsiConsole _console = console ?? throw new ArgumentNullException(nameof(console));
    private readonly ICakeLog _log = log ?? throw new ArgumentNullException(nameof(log));

    public void LogStarting(string harvestOutputBase)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(harvestOutputBase);
        _log.Information("Consolidating harvest RID status files from: {0}", harvestOutputBase);
    }

    public void StartLibrary(string libraryName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryName);
        _console.Write(new Rule($"[yellow]Consolidate: {Markup.Escape(libraryName)}[/]"));
    }

    public void SkipLibraryNoRidStatus(string libraryName, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryName);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        _log.Information("{0} for library: {1}", reason, libraryName);
    }

    public void LogLoadedRidStatuses(string libraryName, int count)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryName);
        _log.Information("Found {0} RID status files for library: {1}", count, libraryName);
    }

    public void FinishLibrary(string libraryName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryName);
        _console.Write(new Rule($"[green]Finished Consolidate: {Markup.Escape(libraryName)}[/]"));
    }

    public void ReportLibraryFailure(string libraryName, string message, Exception? exception)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryName);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        _log.Error("Failed to consolidate harvest for library {0}: {1}", libraryName, message);
        if (exception is not null)
        {
            _log.Verbose("Consolidation error details: {0}", exception);
        }

        _console.Write(new Rule($"[red]Failed Consolidate: {Markup.Escape(libraryName)}[/]"));
    }

    public void WarnCleanupFailure(string libraryName, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryName);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        _log.Warning("Best-effort tmp cleanup failed for {0}: {1}", libraryName, message);
    }

    public void LogCompleted()
    {
        _log.Information("Harvest consolidation completed successfully");
        _console.Write(new Rule("[green]Harvest consolidation completed successfully[/]"));
    }
}
