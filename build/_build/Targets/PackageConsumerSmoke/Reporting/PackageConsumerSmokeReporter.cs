using Cake.Core.Diagnostics;
using Spectre.Console;

namespace Build.Targets.PackageConsumerSmoke.Reporting;

/// <summary>
/// Visual + textual reporting for the PackageConsumerSmoke target. IAnsiConsole + ICakeLog
/// cohort (HarvestReporter + PackageReporter + ConsolidateHarvestReporter pattern). Surfaces
/// stage start/completion banners on the console and per-TFM progress + skip lines on the
/// Cake log. Skip lines carry the reason text so operators see why net462 didn't run on a
/// Mono-less host without digging into source.
/// </summary>
public sealed class PackageConsumerSmokeReporter(IAnsiConsole console, ICakeLog log)
{
    private readonly IAnsiConsole _console = console ?? throw new ArgumentNullException(nameof(console));
    private readonly ICakeLog _log = log ?? throw new ArgumentNullException(nameof(log));

    public void LogStarting(string rid, IReadOnlyList<string> familyNames)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rid);
        ArgumentNullException.ThrowIfNull(familyNames);

        _console.Write(new Rule($"[bold blue]PackageConsumerSmoke[/] [grey]({rid})[/]") { Justification = Justify.Left });
        _log.Information("PackageConsumerSmoke starting on RID '{0}' for families: {1}", rid, string.Join(", ", familyNames));
    }

    public void StartTfm(string tfm)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tfm);
        _log.Information("PackageConsumerSmoke: running TFM '{0}'.", tfm);
    }

    public void FinishTfm(string tfm)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tfm);
        _log.Information("PackageConsumerSmoke: TFM '{0}' completed successfully.", tfm);
    }

    public void ReportSkippedTfm(string tfm, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tfm);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        _log.Warning("PackageConsumerSmoke: skipping TFM '{0}': {1}", tfm, reason);
    }

    public void LogCompleted(int tfmsRun, int tfmsSkipped)
    {
        _console.Write(new Rule($"[bold green]PackageConsumerSmoke complete[/] [grey]({tfmsRun} TFMs run, {tfmsSkipped} skipped)[/]") { Justification = Justify.Left });
        _log.Information("PackageConsumerSmoke completed. TFMs run: {0}, skipped: {1}.", tfmsRun, tfmsSkipped);
    }
}
