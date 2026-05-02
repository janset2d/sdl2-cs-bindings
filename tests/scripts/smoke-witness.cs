#!/usr/bin/env dotnet
#:property TargetFramework=net10.0
#:property TargetFrameworks=
#:property PublishAot=false
#:property NoError=$(NoError);CA1502;CA1505;CA1031;CA1515;CA2007;CA1869;IL2026;IL3050
#:property NoWarn=$(NoWarn);CA1502;CA1505;CA1031;CA1515;CA2007;CA1869;IL2026;IL3050
#:package Spectre.Console

using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Build.Scripts.SmokeWitness;
using Spectre.Console;

var (mode, verbose, baselinePath) = ParseArgs(args);
if (mode is null)
{
    return 2;
}

var context = await InitializeContextAsync(mode.Value, verbose);
PrintHeader(context);

var results = new List<StepResult>();
var totalStopwatch = Stopwatch.StartNew();

try
{
    switch (mode.Value)
    {
        case SmokeMode.Local:
            await RunLocalAsync(context, results);
            break;
        case SmokeMode.CiSim:
            await RunCiSimAsync(context, results);
            break;
        case SmokeMode.Remote:
            await RunRemoteAsync(context, results);
            break;
    }
}
catch (StepFailedException failure)
{
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine($"[red]Halted after '{Markup.Escape(failure.Label)}' (exit {failure.ExitCode}).[/]");
    AnsiConsole.MarkupLine($"[grey]Log:[/] [cyan]{Markup.Escape(failure.LogPath)}[/]");
}

totalStopwatch.Stop();
PrintSummary(results, totalStopwatch.Elapsed, context.LogDir);

if (baselinePath is not null)
{
    await EmitBaselineAsync(context, results, baselinePath);
}

return results.Exists(static r => !r.Succeeded) ? 1 : 0;

static (SmokeMode? Mode, bool Verbose, string? BaselinePath) ParseArgs(string[] args)
{
    var verbose = args.Any(a => a is "--verbose" or "-v");

    // --emit-baseline <path> consumes the next argument as the output path. Track which
    // indices have been claimed so the mode positional lookup below skips over the
    // consumed value (e.g. `--emit-baseline foo.json local` must still resolve mode to
    // `local`, not `foo.json`).
    string? baselinePath = null;
    var consumedIndices = new HashSet<int>();
    for (var i = 0; i < args.Length; i++)
    {
        if (args[i] is not "--emit-baseline")
        {
            continue;
        }

        if (i + 1 >= args.Length)
        {
            AnsiConsole.MarkupLine("[red]--emit-baseline requires a path argument.[/]");
            return (null, verbose, null);
        }

        baselinePath = args[i + 1];
        consumedIndices.Add(i);
        consumedIndices.Add(i + 1);
    }

    // First non-flag positional arg (excluding values consumed by --emit-baseline) is
    // the mode; default to "local".
    string? modeArg = null;
    for (var i = 0; i < args.Length; i++)
    {
        var a = args[i];
        if (a.StartsWith('-') || consumedIndices.Contains(i))
        {
            continue;
        }

        modeArg = a;
        break;
    }

    modeArg ??= "local";
    var mode = modeArg.Trim().ToLowerInvariant() switch
    {
        "local" => (SmokeMode?)SmokeMode.Local,
        "ci-sim" => (SmokeMode?)SmokeMode.CiSim,
        "remote" => (SmokeMode?)SmokeMode.Remote,
        var raw => PrintUnknownMode(raw),
    };

    return (mode, verbose, baselinePath);

    static SmokeMode? PrintUnknownMode(string raw)
    {
        AnsiConsole.MarkupLine($"[red]Unknown mode '{Markup.Escape(raw)}'. Expected 'local', 'remote', or 'ci-sim'.[/]");
        AnsiConsole.MarkupLine("  [grey]local[/]    → CleanArtifacts → SetupLocalDev (--source=local) → PackageConsumerSmoke (default; fast dev iterate)");
        AnsiConsole.MarkupLine("  [grey]remote[/]   → CleanArtifacts → SetupLocalDev (--source=remote) → PackageConsumerSmoke (test against published GH Packages feed; needs GH_TOKEN env)");
        AnsiConsole.MarkupLine("  [grey]ci-sim[/]   → mini CI replay: every stage invoked standalone with ResolveVersions mapping + ConsumerSmoke");
        AnsiConsole.MarkupLine("  [grey]-v/--verbose[/] → tee each step's stdout/stderr to the console (default: off; output always written to log files)");
        AnsiConsole.MarkupLine("  [grey]--emit-baseline <path>[/] → after the run, write a deterministic JSON behavior signal (mode/host_rid/step labels+exit codes) to <path>; phase-x P0 deliverable (ADR-004 plan §2.1.2)");
        return null;
    }
}

static async Task<WitnessContext> InitializeContextAsync(SmokeMode mode, bool verbose)
{
    var repoRoot = await FindRepoRootAsync();
    var runId = DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);
    var platform = ResolvePlatformTag();
    var modeTag = mode switch
    {
        SmokeMode.Local => "local",
        SmokeMode.Remote => "remote",
        SmokeMode.CiSim => "ci-sim",
        _ => throw new InvalidOperationException($"Unhandled smoke mode '{mode}'."),
    };

    // Logs live under `.logs/witness/` at the repo root (gitignored) rather than
    // `artifacts/` so Cake's CleanArtifacts target cannot wipe them mid-run. Clear
    // any stale directory at the same path, then recreate for a guaranteed clean
    // slate even if two runs collide on the same second-level timestamp.
    var logDir = Path.Combine(repoRoot, ".logs", "witness", $"{platform}-{modeTag}-{runId}");
    if (Directory.Exists(logDir))
    {
        Directory.Delete(logDir, recursive: true);
    }

    Directory.CreateDirectory(logDir);

    var hostRid = ResolveHostRid();
    var headSha = await TryGetHeadShaAsync(repoRoot);
    return new WitnessContext(mode, repoRoot, logDir, hostRid, platform, runId, headSha, verbose);
}

static async Task RunLocalAsync(WitnessContext ctx, List<StepResult> results)
{
    await StepAsync(ctx, results, "CleanArtifacts", ["--target", "CleanArtifacts"]);
    await StepAsync(ctx, results, "SetupLocalDev", ["--target", "SetupLocalDev", "--source=local"]);

    // SetupLocalDev emits artifacts/resolve-versions/versions.json (C.11a).
    // Cake's --versions-file reads the JSON directly — no client-side family
    // filtering needed; PackageConsumerSmokeRunner already filters to concrete
    // families (managed_project + native_project != null).
    var versionsFile = Path.Combine(ctx.RepoRoot, "artifacts", "resolve-versions", "versions.json");
    await PrintResolvedVersionsFromFileAsync(versionsFile);

    await StepAsync(ctx, results, "PackageConsumerSmoke",
        ["--target", "PackageConsumerSmoke", "--rid", ctx.HostRid, "--versions-file", versionsFile]);
}

static async Task RunRemoteAsync(WitnessContext ctx, List<StepResult> results)
{
    await StepAsync(ctx, results, "CleanArtifacts", ["--target", "CleanArtifacts"]);
    await StepAsync(ctx, results, "SetupLocalDev (remote)", ["--target", "SetupLocalDev", "--source=remote"]);

    // RemoteArtifactSourceResolver writes versions.json from its discovered mapping
    // (parity with the Local profile), so the consumer-smoke step routes through the
    // same --versions-file flag in both modes.
    var versionsFile = Path.Combine(ctx.RepoRoot, "artifacts", "resolve-versions", "versions.json");
    await PrintResolvedVersionsFromFileAsync(versionsFile);

    await StepAsync(ctx, results, "PackageConsumerSmoke",
        ["--target", "PackageConsumerSmoke", "--rid", ctx.HostRid, "--versions-file", versionsFile]);
}

static async Task RunCiSimAsync(WitnessContext ctx, List<StepResult> results)
{
    await StepAsync(ctx, results, "CleanArtifacts", ["--target", "CleanArtifacts"]);

    var suffix = string.Create(
        CultureInfo.InvariantCulture,
        $"local.{DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmss", CultureInfo.InvariantCulture)}");

    await StepAsync(
        ctx,
        results,
        "ResolveVersions",
        [
            "--target", "ResolveVersions",
            "--version-source=manifest",
            $"--suffix={suffix}",
        ]);

    // Cake's --versions-file reads the JSON directly — concrete-family filtering
    // lives inside each Cake runner (PackageTaskRunner, PreflightTaskRunner,
    // PackageConsumerSmokeRunner), not in the CI/witness wiring layer.
    var versionsFile = Path.Combine(ctx.RepoRoot, "artifacts", "resolve-versions", "versions.json");
    await PrintResolvedVersionsFromFileAsync(versionsFile);

    await StepAsync(ctx, results, "PreFlightCheck", ["--target", "PreFlightCheck", "--versions-file", versionsFile]);
    await StepAsync(ctx, results, "EnsureVcpkgDependencies", ["--target", "EnsureVcpkgDependencies", "--rid", ctx.HostRid]);
    await StepAsync(ctx, results, "Harvest", ["--target", "Harvest", "--rid", ctx.HostRid]);
    await StepAsync(ctx, results, "NativeSmoke", ["--target", "NativeSmoke", "--rid", ctx.HostRid]);
    await StepAsync(ctx, results, "ConsolidateHarvest", ["--target", "ConsolidateHarvest"]);
    await StepAsync(ctx, results, "Package", ["--target", "Package", "--versions-file", versionsFile]);
    await StepAsync(ctx, results, "PackageConsumerSmoke", ["--target", "PackageConsumerSmoke", "--rid", ctx.HostRid, "--versions-file", versionsFile]);
}

static async Task StepAsync(WitnessContext ctx, List<StepResult> results, string label, string[] cakeArgs)
{
    var stepNumber = results.Count + 1;
    AnsiConsole.Write(new Rule($"[yellow]{stepNumber:00}. {Markup.Escape(label)}[/]").RuleStyle("grey").LeftJustified());

    var fullArgs = new List<string>(5 + cakeArgs.Length)
    {
        "run",
        "--project", Path.Combine(ctx.RepoRoot, "build", "_build"),
        "-c", "Release",
        "--",
    };
    fullArgs.AddRange(cakeArgs);

    var displayCommand = "dotnet " + string.Join(' ', fullArgs.Select(QuoteIfNeeded));
    AnsiConsole.MarkupLine($"[grey]cmd:[/] [cyan]{Markup.Escape(displayCommand)}[/]");

    var logPath = Path.Combine(ctx.LogDir, $"{stepNumber:00}-{SanitizeForFile(label)}.log");
    var stopwatch = Stopwatch.StartNew();
    var exitCode = await InvokeProcessAsync("dotnet", fullArgs, ctx.RepoRoot, logPath, ctx.Verbose);
    stopwatch.Stop();

    var result = new StepResult(label, exitCode, stopwatch.Elapsed, logPath);
    results.Add(result);

    if (!result.Succeeded)
    {
        AnsiConsole.MarkupLine($"[red]FAIL[/] ({stopwatch.Elapsed.TotalSeconds:F1}s) → [cyan]{Markup.Escape(logPath)}[/]");
        AnsiConsole.WriteLine();
        throw new StepFailedException(label, exitCode, logPath);
    }

    AnsiConsole.MarkupLine($"[green]OK[/]   ({stopwatch.Elapsed.TotalSeconds:F1}s) → [grey]{Markup.Escape(logPath)}[/]");
    AnsiConsole.WriteLine();
}

static async Task<int> InvokeProcessAsync(string fileName, IEnumerable<string> arguments, string workingDirectory, string logPath, bool verbose = false)
{
    var argumentList = arguments as IList<string> ?? arguments.ToList();

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

    foreach (var arg in argumentList)
        startInfo.ArgumentList.Add(arg);

    using var process = new Process { StartInfo = startInfo };
    process.Start();

    var (stdout, stderr) = verbose
        ? await DrainVerboselyAsync(process)
        : await DrainSilentlyAsync(process);

    // Always write a log file regardless of the verbose flag; silent runs need forensic
    // evidence on disk just as much as (more than) verbose ones — verbose-rerun-required-
    // to-debug a flaky failure was a footgun (the failing run's evidence was lost the
    // moment the process exited).
    await WriteLogAsync(fileName, argumentList, workingDirectory, logPath, process.ExitCode, stdout, stderr);
    return process.ExitCode;
}

static async Task<(string Stdout, string Stderr)> DrainSilentlyAsync(Process process)
{
    // Silent: drain streams to prevent deadlock, no console echo. Output captured for
    // the log file written by the caller.
    var outTask = process.StandardOutput.ReadToEndAsync();
    var errTask = process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync();
    var stdout = await outTask;
    var stderr = await errTask;
    return (stdout, stderr);
}

static async Task<(string Stdout, string Stderr)> DrainVerboselyAsync(Process process)
{
    // Verbose: stream each line live to console + capture for log.
    // Use Console.Out directly — Cake's ANSI sequences would be misinterpreted by Spectre.
    var stdoutSb = new StringBuilder();
    var stderrSb = new StringBuilder();
    var stdoutDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var stderrDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    process.OutputDataReceived += (_, e) =>
    {
        if (e.Data is null) { stdoutDone.TrySetResult(); return; }
        stdoutSb.AppendLine(e.Data);
        Console.Out.WriteLine(e.Data);
    };

    process.ErrorDataReceived += (_, e) =>
    {
        if (e.Data is null) { stderrDone.TrySetResult(); return; }
        stderrSb.AppendLine(e.Data);
        Console.Error.WriteLine(e.Data);
    };

    process.BeginOutputReadLine();
    process.BeginErrorReadLine();
    await process.WaitForExitAsync();
    await Task.WhenAll(stdoutDone.Task, stderrDone.Task);

    return (stdoutSb.ToString(), stderrSb.ToString());
}

static async Task WriteLogAsync(string fileName, IList<string> argumentList, string workingDirectory, string logPath, int exitCode, string stdout, string stderr)
{
    var header = new StringBuilder();
    header.Append("$ ").Append(fileName).Append(' ').AppendJoin(' ', argumentList).AppendLine();
    header.Append("# cwd=").AppendLine(workingDirectory);
    header.AppendLine();

    var body = new StringBuilder(header.Length + stdout.Length + stderr.Length + 128);
    body.Append(header);
    body.Append(stdout);
    if (stderr.Length > 0)
    {
        body.AppendLine();
        body.AppendLine("# ---- stderr ----");
        body.Append(stderr);
    }
    body.AppendLine();
    body.Append("# exit=").Append(exitCode)
        .Append(' ').Append("finished=").AppendLine(DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));

    await File.WriteAllTextAsync(logPath, body.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
}

static async Task PrintResolvedVersionsFromFileAsync(string versionsFilePath)
{
    if (!File.Exists(versionsFilePath))
    {
        AnsiConsole.MarkupLine($"[yellow]versions.json not found at '{Markup.Escape(versionsFilePath)}' — Cake will surface the error.[/]");
        return;
    }

    var json = await File.ReadAllTextAsync(versionsFilePath);
    using var doc = JsonDocument.Parse(json);
    AnsiConsole.MarkupLine($"[grey]Resolved mapping (--versions-file {Markup.Escape(Path.GetFileName(versionsFilePath))}):[/]");
    foreach (var prop in doc.RootElement.EnumerateObject().OrderBy(static p => p.Name, StringComparer.OrdinalIgnoreCase))
    {
        AnsiConsole.MarkupLine($"  [cyan]{Markup.Escape(prop.Name)}[/]=[yellow]{Markup.Escape(prop.Value.GetString() ?? "(null)")}[/]");
    }

    AnsiConsole.WriteLine();
}

static void PrintHeader(WitnessContext ctx)
{
    AnsiConsole.Write(new FigletText("smoke-witness").Color(Color.Yellow));

    var table = new Table().NoBorder().HideHeaders();
    table.AddColumn(new TableColumn(string.Empty).PadRight(2));
    table.AddColumn(new TableColumn(string.Empty));
    var modeLabel = ctx.Mode switch
    {
        SmokeMode.Local => "local",
        SmokeMode.Remote => "remote",
        SmokeMode.CiSim => "ci-sim",
        _ => "unknown",
    };
    table.AddRow("[grey]Mode[/]", $"[cyan]{Markup.Escape(modeLabel)}[/]");
    table.AddRow("[grey]Platform[/]", $"[cyan]{Markup.Escape(ctx.Platform)}[/] ([cyan]{Markup.Escape(ctx.HostRid)}[/])");
    table.AddRow("[grey]Repo[/]", $"[cyan]{Markup.Escape(ctx.RepoRoot)}[/]");
    table.AddRow("[grey]HEAD[/]", $"[cyan]{Markup.Escape(ctx.HeadSha ?? "unknown")}[/]");
    table.AddRow("[grey]Logs[/]", $"[cyan]{Markup.Escape(ctx.LogDir)}[/]");
    table.AddRow("[grey]Run ID[/]", $"[cyan]{Markup.Escape(ctx.RunId)}[/]");
    table.AddRow("[grey]Verbose[/]", ctx.Verbose ? "[yellow]on (--verbose)[/]" : "[grey]off[/]");
    AnsiConsole.Write(table);
    AnsiConsole.WriteLine();
}

static void PrintSummary(List<StepResult> results, TimeSpan totalDuration, string logDir)
{
    var table = new Table().RoundedBorder().BorderColor(Color.Grey);
    table.AddColumn("#");
    table.AddColumn("Stage");
    table.AddColumn(new TableColumn("Status").Centered());
    table.AddColumn(new TableColumn("Duration").RightAligned());

    for (var i = 0; i < results.Count; i++)
    {
        var r = results[i];
        var status = r.Succeeded ? "[green]PASS[/]" : $"[red]FAIL (exit {r.ExitCode})[/]";
        table.AddRow(
            (i + 1).ToString(CultureInfo.InvariantCulture),
            Markup.Escape(r.Label),
            status,
            $"{r.Duration.TotalSeconds:F1}s");
    }

    var failed = results.Count(static r => !r.Succeeded);
    var passed = results.Count - failed;
    var headerColor = failed == 0 ? "green" : "red";
    var headerText = failed == 0 ? "All stages passed" : $"{failed} stage(s) failed";

    AnsiConsole.Write(new Panel(table)
        .Header($"[{headerColor}]{headerText}[/] — [grey]{passed}/{results.Count} passed · total {totalDuration.TotalSeconds:F1}s[/]")
        .BorderColor(failed == 0 ? Color.Green : Color.Red));

    if (failed > 0)
    {
        AnsiConsole.MarkupLine($"[yellow]Inspect failing logs under[/] [cyan]{Markup.Escape(logDir)}[/].");
    }
}

static async Task<string> FindRepoRootAsync()
{
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

    return output;
}

static string ResolvePlatformTag()
{
    if (OperatingSystem.IsWindows())
    {
        return "windows";
    }

    if (OperatingSystem.IsLinux())
    {
        return "linux";
    }

    if (OperatingSystem.IsMacOS())
    {
        return "macos";
    }

    return "unknown";
}

static string ResolveHostRid()
{
    string os;
    if (OperatingSystem.IsWindows())
    {
        os = "win";
    }
    else if (OperatingSystem.IsLinux())
    {
        os = "linux";
    }
    else if (OperatingSystem.IsMacOS())
    {
        os = "osx";
    }
    else
    {
        throw new PlatformNotSupportedException("Unknown OS for RID derivation.");
    }

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

static async Task EmitBaselineAsync(WitnessContext ctx, List<StepResult> results, string baselinePath)
{
    // Behavior signal per phase-x plan §2.1.2: deterministic ordered tuple of
    // (step label, exit code) plus mode + host RID. Wave commits compare the
    // baseline-before vs baseline-after; strict equality is the green criterion.
    // Step duration, log path, and console output are intentionally excluded
    // because they are non-deterministic (timestamps, runIds, Cake progress).
    var modeLabel = ctx.Mode switch
    {
        SmokeMode.Local => "local",
        SmokeMode.Remote => "remote",
        SmokeMode.CiSim => "ci-sim",
        _ => "unknown",
    };

    var baseline = new BaselineSignal(
        Mode: modeLabel,
        HostRid: ctx.HostRid,
        StepCount: results.Count,
        Steps: [.. results.Select(r => new BaselineStep(r.Label, r.ExitCode))],
        Passed: results.Count(static r => r.Succeeded),
        Failed: results.Count(static r => !r.Succeeded));

    var directory = Path.GetDirectoryName(baselinePath);
    if (!string.IsNullOrEmpty(directory))
    {
        Directory.CreateDirectory(directory);
    }

    var options = new JsonSerializerOptions
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };
    var json = JsonSerializer.Serialize(baseline, options);
    await File.WriteAllTextAsync(baselinePath, json + Environment.NewLine, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

    AnsiConsole.MarkupLine($"[grey]Baseline signal written:[/] [cyan]{Markup.Escape(baselinePath)}[/]");
}

static async Task<string?> TryGetHeadShaAsync(string repoRoot)
{
    try
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = "rev-parse --short HEAD",
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return null;
        }

        var output = (await process.StandardOutput.ReadToEndAsync()).Trim();
        await process.WaitForExitAsync();
        return process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output) ? output : null;
    }
    catch (Exception)
    {
        return null;
    }
}

static string SanitizeForFile(string value)
{
    var sanitized = new StringBuilder(value.Length);
    foreach (var ch in value)
    {
        sanitized.Append(char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '-');
    }

    return sanitized.ToString();
}

static string QuoteIfNeeded(string argument)
{
    return argument.Contains(' ', StringComparison.Ordinal) ? $"\"{argument}\"" : argument;
}

namespace Build.Scripts.SmokeWitness
{
    internal enum SmokeMode
    {
        Local,
        CiSim,
        Remote,
    }

    internal sealed record WitnessContext(
        SmokeMode Mode,
        string RepoRoot,
        string LogDir,
        string HostRid,
        string Platform,
        string RunId,
        string? HeadSha,
        bool Verbose);

    internal sealed record StepResult(string Label, int ExitCode, TimeSpan Duration, string LogPath)
    {
        public bool Succeeded => ExitCode == 0;
    }

    internal sealed record BaselineSignal(
        string Mode,
        string HostRid,
        int StepCount,
        IReadOnlyList<BaselineStep> Steps,
        int Passed,
        int Failed);

    internal sealed record BaselineStep(string Label, int Exit);

    public sealed class StepFailedException : Exception
    {
        public StepFailedException()
        {
            Label = string.Empty;
            LogPath = string.Empty;
        }

        public StepFailedException(string message)
            : base(message)
        {
            Label = string.Empty;
            LogPath = string.Empty;
        }

        public StepFailedException(string message, Exception innerException)
            : base(message, innerException)
        {
            Label = string.Empty;
            LogPath = string.Empty;
        }

        public StepFailedException(string label, int exitCode, string logPath)
            : base($"Step '{label}' failed with exit code {exitCode}. See {logPath}.")
        {
            Label = label;
            ExitCode = exitCode;
            LogPath = logPath;
        }

        public string Label { get; }

        public int ExitCode { get; }

        public string LogPath { get; }
    }
}
