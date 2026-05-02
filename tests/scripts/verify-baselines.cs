#!/usr/bin/env dotnet
#:property TargetFramework=net10.0
#:property TargetFrameworks=
#:property PublishAot=false
#:property NoError=$(NoError);CA1502;CA1505;CA1031;CA1515;CA2007;CA1812;CA1869;IL2026;IL3050
#:property NoWarn=$(NoWarn);CA1502;CA1505;CA1031;CA1515;CA2007;CA1812;CA1869;IL2026;IL3050
#:package Spectre.Console

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Build.Scripts.VerifyBaselines;
using Spectre.Console;

// verify-baselines.cs — operationalizes the phase-x ADR-004 plan §2.1.5 fast/milestone
// loop cadence. Default invocation runs the fast loop (Windows local, host-matched).
// --milestone runs all milestone entries reachable from the current host.
//
// Each entry spawns `smoke-witness.cs <mode> --emit-baseline <tmp>` and then compares
// the emitted JSON tuple ((mode, host_rid, [(label, exit), ...])) against the
// committed baseline file via the same JsonSerializer options. Strict logical equality
// is the green criterion; any mismatch is wave-rejection signal per phase plan §12.3.

var (mode, keepTmp) = ParseArgs(args);
var workingDir = await ResolveScriptDirectoryAsync();
var hostRid = ResolveHostRid();

PrintHeader(mode, hostRid, workingDir);

var entries = BuildEntries(mode, hostRid);
var results = new List<EntryResult>();

foreach (var entry in entries)
{
    results.Add(await VerifyEntryAsync(entry, workingDir, hostRid, keepTmp));
}

PrintSummary(results);

// Exit zero only when every NON-SKIPPED entry matched. Skipped entries (host mismatch
// or baseline missing) do not flip the gate red — they are reported but tolerated.
var hadFailure = results.Exists(static r => r.Status is EntryStatus.Mismatch or EntryStatus.SpawnFailure);
return hadFailure ? 1 : 0;

static (LoopMode Mode, bool KeepTmp) ParseArgs(string[] args)
{
    var milestone = args.Any(a => a is "--milestone");
    var keepTmp = args.Any(a => a is "--keep-tmp");
    return (milestone ? LoopMode.Milestone : LoopMode.Fast, keepTmp);
}

static async Task<string> ResolveScriptDirectoryAsync()
{
    // Ground truth: git rev-parse --show-toplevel + tests/scripts subdir. Avoids
    // Assembly.Location (IL3000 in single-file context) and AppContext.BaseDirectory
    // (returns the runfile bin/ tree, not the source dir we need to spawn from).
    var startInfo = new ProcessStartInfo
    {
        FileName = "git",
        Arguments = "rev-parse --show-toplevel",
        RedirectStandardOutput = true,
        UseShellExecute = false,
        CreateNoWindow = true,
    };

    using var process = Process.Start(startInfo)
        ?? throw new InvalidOperationException("Failed to start 'git rev-parse --show-toplevel'. Is git on PATH?");

    var output = (await process.StandardOutput.ReadToEndAsync()).Trim();
    await process.WaitForExitAsync();

    if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
    {
        throw new InvalidOperationException("Could not resolve repository root via git. Run this script from inside the repo.");
    }

    return Path.Combine(output, "tests", "scripts");
}

static string ResolveHostRid()
{
    string os;
    if (OperatingSystem.IsWindows()) { os = "win"; }
    else if (OperatingSystem.IsLinux()) { os = "linux"; }
    else if (OperatingSystem.IsMacOS()) { os = "osx"; }
    else { throw new PlatformNotSupportedException("Unknown OS for RID derivation."); }

    var arch = RuntimeInformation.OSArchitecture switch
    {
        Architecture.X64 => "x64",
        Architecture.Arm64 => "arm64",
        Architecture.X86 => "x86",
        Architecture.Arm => "arm",
        var other => throw new PlatformNotSupportedException($"Unknown architecture '{other}' for RID derivation."),
    };

    return $"{os}-{arch}";
}

static IReadOnlyList<Entry> BuildEntries(LoopMode mode, string hostRid)
{
    // Fast-loop entry: the host-matched local baseline is always present.
    var entries = new List<Entry>
    {
        new("local", hostRid, $"smoke-witness-local-{hostRid}.json"),
    };

    if (mode != LoopMode.Milestone)
    {
        return entries;
    }

    // Milestone-loop: add every other-host / other-mode entry beyond the fast-loop
    // host-matched local. The runtime gate inside VerifyEntryAsync skips entries whose
    // target RID does not match the current host (cross-host verification is meaningless
    // — a Windows host cannot reproduce a Linux byte-equal signal). Dedup against the
    // fast-loop entry so the host-matched local doesn't run twice when --milestone is
    // invoked on a host whose RID also appears in the milestone catalog (e.g., Linux
    // running --milestone would otherwise verify smoke-witness-local-linux-x64.json
    // both as fast-loop and as milestone-loop entry).
    var milestoneCatalog = new (string Mode, string TargetRid, string BaselineFile)[]
    {
        ("local",  "linux-x64", "smoke-witness-local-linux-x64.json"),
        ("local",  "osx-x64",   "smoke-witness-local-osx-x64.json"),
        ("ci-sim", "win-x64",   "smoke-witness-ci-sim-win-x64.json"),
    };

    foreach (var (catalogMode, catalogRid, catalogFile) in milestoneCatalog)
    {
        var alreadyAdded = entries.Any(e =>
            string.Equals(e.Mode, catalogMode, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(e.TargetRid, catalogRid, StringComparison.OrdinalIgnoreCase));
        if (!alreadyAdded)
        {
            entries.Add(new(catalogMode, catalogRid, catalogFile));
        }
    }

    return entries;
}

static async Task<EntryResult> VerifyEntryAsync(Entry entry, string workingDir, string hostRid, bool keepTmp)
{
    var label = $"{entry.Mode} ({entry.TargetRid})";
    var baselinePath = Path.Combine(workingDir, "baselines", entry.BaselineFile);

    if (!string.Equals(entry.TargetRid, hostRid, StringComparison.OrdinalIgnoreCase))
    {
        return new EntryResult(label, EntryStatus.SkippedHostMismatch, $"current host is {hostRid}");
    }

    if (!File.Exists(baselinePath))
    {
        return new EntryResult(label, EntryStatus.SkippedMissingBaseline, $"baseline not committed: {entry.BaselineFile}");
    }

    var tmpDir = Path.Combine(Path.GetTempPath(), "verify-baselines", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tmpDir);
    var tmpBaseline = Path.Combine(tmpDir, "actual.json");

    try
    {
        return await SpawnAndDiffAsync(entry, label, baselinePath, tmpBaseline, workingDir);
    }
    finally
    {
        if (keepTmp)
        {
            AnsiConsole.MarkupLine($"[grey]  tmp baseline kept at:[/] [cyan]{Markup.Escape(tmpBaseline)}[/]");
        }
        else
        {
            TryDeleteDir(tmpDir);
        }
    }
}

static async Task<EntryResult> SpawnAndDiffAsync(Entry entry, string label, string baselinePath, string tmpBaseline, string workingDir)
{
    AnsiConsole.MarkupLine($"[grey]Running:[/] [cyan]smoke-witness.cs {Markup.Escape(entry.Mode)}[/] → comparing to [grey]{Markup.Escape(entry.BaselineFile)}[/]");

    var smokeArgs = new[]
    {
        "run",
        "smoke-witness.cs",
        entry.Mode,
        "--emit-baseline", tmpBaseline,
    };

    var stopwatch = Stopwatch.StartNew();
    int spawnExit;
    try
    {
        spawnExit = await SpawnAsync("dotnet", smokeArgs, workingDir);
    }
    catch (Exception ex)
    {
        return new EntryResult(label, EntryStatus.SpawnFailure, $"smoke-witness spawn threw: {ex.Message}");
    }

    stopwatch.Stop();

    if (!File.Exists(tmpBaseline))
    {
        return new EntryResult(label, EntryStatus.SpawnFailure, $"smoke-witness exited {spawnExit}; no baseline emitted");
    }

    var (expected, actual, deserializeError) = await TryReadBothAsync(baselinePath, tmpBaseline);
    if (deserializeError is not null)
    {
        return new EntryResult(label, EntryStatus.SpawnFailure, $"deserialize failed: {deserializeError}");
    }

    var diff = DiffSignals(expected!, actual!);
    return diff is null
        ? new EntryResult(label, EntryStatus.Match, $"{stopwatch.Elapsed.TotalSeconds:F1}s")
        : new EntryResult(label, EntryStatus.Mismatch, diff);
}

static async Task<(BaselineSignal? Expected, BaselineSignal? Actual, string? Error)> TryReadBothAsync(string baselinePath, string tmpBaseline)
{
    var options = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    try
    {
        await using var expectedStream = File.OpenRead(baselinePath);
        var expected = await JsonSerializer.DeserializeAsync<BaselineSignal>(expectedStream, options)
            ?? throw new InvalidOperationException("expected baseline deserialized to null");

        await using var actualStream = File.OpenRead(tmpBaseline);
        var actual = await JsonSerializer.DeserializeAsync<BaselineSignal>(actualStream, options)
            ?? throw new InvalidOperationException("actual baseline deserialized to null");

        return (expected, actual, null);
    }
    catch (Exception ex)
    {
        return (null, null, ex.Message);
    }
}

static string? DiffSignals(BaselineSignal expected, BaselineSignal actual)
{
    var problems = new List<string>();

    if (!string.Equals(expected.Mode, actual.Mode, StringComparison.Ordinal))
    {
        problems.Add($"mode: expected={expected.Mode}, actual={actual.Mode}");
    }

    if (!string.Equals(expected.HostRid, actual.HostRid, StringComparison.Ordinal))
    {
        problems.Add($"host_rid: expected={expected.HostRid}, actual={actual.HostRid}");
    }

    if (expected.StepCount != actual.StepCount)
    {
        problems.Add($"step_count: expected={expected.StepCount}, actual={actual.StepCount}");
    }

    var stepCompareCount = Math.Min(expected.Steps.Count, actual.Steps.Count);
    for (var i = 0; i < stepCompareCount; i++)
    {
        var e = expected.Steps[i];
        var a = actual.Steps[i];
        if (!string.Equals(e.Label, a.Label, StringComparison.Ordinal))
        {
            problems.Add($"step[{i}].label: expected='{e.Label}', actual='{a.Label}'");
        }

        if (e.Exit != a.Exit)
        {
            problems.Add($"step[{i}].exit ({e.Label}): expected={e.Exit}, actual={a.Exit}");
        }
    }

    if (expected.Steps.Count != actual.Steps.Count)
    {
        problems.Add($"step list length differs ({expected.Steps.Count} vs {actual.Steps.Count})");
    }

    if (expected.Passed != actual.Passed || expected.Failed != actual.Failed)
    {
        problems.Add($"passed/failed: expected={expected.Passed}/{expected.Failed}, actual={actual.Passed}/{actual.Failed}");
    }

    return problems.Count == 0 ? null : string.Join("; ", problems);
}

static async Task<int> SpawnAsync(string fileName, IEnumerable<string> arguments, string workingDirectory)
{
    var startInfo = new ProcessStartInfo
    {
        FileName = fileName,
        WorkingDirectory = workingDirectory,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true,
        StandardOutputEncoding = Encoding.UTF8,
        StandardErrorEncoding = Encoding.UTF8,
    };

    foreach (var arg in arguments)
    {
        startInfo.ArgumentList.Add(arg);
    }

    using var process = new Process { StartInfo = startInfo };
    process.Start();

    // Drain streams to prevent deadlock; smoke-witness writes its own .logs/witness/...
    // log files anyway, so we do not surface its stdout/stderr inline.
    var outTask = process.StandardOutput.ReadToEndAsync();
    var errTask = process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync();
    await Task.WhenAll(outTask, errTask);
    return process.ExitCode;
}

static void TryDeleteDir(string path)
{
    if (!Directory.Exists(path))
    {
        return;
    }

    try
    {
        Directory.Delete(path, recursive: true);
    }
    catch (IOException ex)
    {
        AnsiConsole.MarkupLine($"[grey]  tmp cleanup skipped ({Markup.Escape(ex.GetType().Name)}): {Markup.Escape(path)}[/]");
    }
    catch (UnauthorizedAccessException ex)
    {
        AnsiConsole.MarkupLine($"[grey]  tmp cleanup skipped ({Markup.Escape(ex.GetType().Name)}): {Markup.Escape(path)}[/]");
    }
}

static void PrintHeader(LoopMode mode, string hostRid, string workingDir)
{
    AnsiConsole.Write(new FigletText("verify-baselines").Color(Color.Yellow));
    var table = new Table().NoBorder().HideHeaders();
    table.AddColumn(new TableColumn(string.Empty).PadRight(2));
    table.AddColumn(new TableColumn(string.Empty));
    table.AddRow("[grey]Loop[/]", $"[cyan]{mode.ToString().ToLowerInvariant()}[/]");
    table.AddRow("[grey]Host RID[/]", $"[cyan]{Markup.Escape(hostRid)}[/]");
    table.AddRow("[grey]Scripts dir[/]", $"[cyan]{Markup.Escape(workingDir)}[/]");
    AnsiConsole.Write(table);
    AnsiConsole.WriteLine();
}

static void PrintSummary(List<EntryResult> results)
{
    var table = new Table().RoundedBorder().BorderColor(Color.Grey);
    table.AddColumn("Entry");
    table.AddColumn(new TableColumn("Status").Centered());
    table.AddColumn("Detail");

    foreach (var r in results)
    {
        var status = r.Status switch
        {
            EntryStatus.Match => "[green]MATCH[/]",
            EntryStatus.Mismatch => "[red]MISMATCH[/]",
            EntryStatus.SkippedHostMismatch => "[grey]SKIP (host)[/]",
            EntryStatus.SkippedMissingBaseline => "[yellow]SKIP (no baseline)[/]",
            EntryStatus.SpawnFailure => "[red]SPAWN FAIL[/]",
            _ => "[red]?[/]",
        };

        table.AddRow(Markup.Escape(r.Label), status, Markup.Escape(r.Detail));
    }

    var failures = results.Count(static r => r.Status is EntryStatus.Mismatch or EntryStatus.SpawnFailure);
    var headerColor = failures == 0 ? "green" : "red";
    var headerText = failures == 0
        ? "All entries match (skipped tolerated)"
        : $"{failures} entry(ies) failed verification";

    AnsiConsole.Write(new Panel(table)
        .Header($"[{headerColor}]{headerText}[/]")
        .BorderColor(failures == 0 ? Color.Green : Color.Red));
}

namespace Build.Scripts.VerifyBaselines
{
    internal enum LoopMode
    {
        Fast,
        Milestone,
    }

    internal enum EntryStatus
    {
        Match,
        Mismatch,
        SkippedHostMismatch,
        SkippedMissingBaseline,
        SpawnFailure,
    }

    internal sealed record Entry(string Mode, string TargetRid, string BaselineFile);

    internal sealed record EntryResult(string Label, EntryStatus Status, string Detail);

    internal sealed record BaselineSignal(
        string Mode,
        string HostRid,
        int StepCount,
        IReadOnlyList<BaselineStep> Steps,
        int Passed,
        int Failed);

    internal sealed record BaselineStep(string Label, int Exit);
}
