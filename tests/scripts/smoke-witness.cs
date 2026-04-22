#!/usr/bin/env dotnet
#:property TargetFramework=net10.0
#:property TargetFrameworks=
#:property PublishAot=false
#:property NoError=$(NoError);CA1502;CA1505
#:property NoWarn=$(NoWarn);CA1502;CA1505
#:package Spectre.Console

using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Build.Scripts.SmokeWitness;
using Spectre.Console;

var (mode, verbose) = ParseArgs(args);
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

return results.Exists(static r => !r.Succeeded) ? 1 : 0;

static (SmokeMode? Mode, bool Verbose) ParseArgs(string[] args)
{
    var verbose = args.Any(a => a is "--verbose" or "-v");

    // First non-flag positional arg is the mode; default to "local".
    var modeArg = args.FirstOrDefault(a => !a.StartsWith('-')) ?? "local";
    var mode = modeArg.Trim().ToLowerInvariant() switch
    {
        "local" => (SmokeMode?)SmokeMode.Local,
        "ci-sim" => (SmokeMode?)SmokeMode.CiSim,
        var raw => PrintUnknownMode(raw),
    };

    return (mode, verbose);

    static SmokeMode? PrintUnknownMode(string raw)
    {
        AnsiConsole.MarkupLine($"[red]Unknown mode '{Markup.Escape(raw)}'. Expected 'local' or 'ci-sim'.[/]");
        AnsiConsole.MarkupLine("  [grey]local[/]    → CleanArtifacts → SetupLocalDev → PackageConsumerSmoke (default; fast dev iterate)");
        AnsiConsole.MarkupLine("  [grey]ci-sim[/]   → mini CI replay: every stage invoked standalone with ResolveVersions mapping + ConsumerSmoke");
        AnsiConsole.MarkupLine("  [grey]-v/--verbose[/] → tee each step's stdout/stderr to the console (default: off; output always written to log files)");
        return null;
    }
}

static async Task<WitnessContext> InitializeContextAsync(SmokeMode mode, bool verbose)
{
    var repoRoot = await FindRepoRootAsync();
    var runId = DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);
    var platform = ResolvePlatformTag();
    var modeTag = mode == SmokeMode.Local ? "local" : "ci-sim";

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

    // SetupLocalDev now emits artifacts/resolve-versions/versions.json (C.11a).
    // Read it, filter to concrete families, and thread --explicit-version into ConsumerSmoke.
    var allVersions = await ParseResolvedVersionsAsync(ctx.RepoRoot);
    var concreteFamilies = await LoadConcreteFamiliesAsync(ctx.RepoRoot);
    var versions = allVersions
        .Where(kvp => concreteFamilies.Contains(kvp.Key))
        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);

    PrintResolvedMapping(versions);

    var versionArgs = versions
        .SelectMany(kvp => new[] { "--explicit-version", $"{kvp.Key}={kvp.Value}" })
        .ToArray();

    await StepAsync(ctx, results, "PackageConsumerSmoke", ["--target", "PackageConsumerSmoke", "--rid", ctx.HostRid, .. versionArgs]);
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

    var allVersions = await ParseResolvedVersionsAsync(ctx.RepoRoot);
    var concreteFamilies = await LoadConcreteFamiliesAsync(ctx.RepoRoot);

    // Downstream stages (Preflight's upstream-alignment validator, Package's
    // family selector) reject families without both managed_project + native_project
    // per manifest. ResolveVersions emits every family (including placeholders like
    // sdl2-net); mirror SetupLocalDevTaskRunner's concrete-family filter so the
    // mini CI replay drives the same set CI's release.yml pack job would.
    var versions = allVersions
        .Where(kvp => concreteFamilies.Contains(kvp.Key))
        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);

    PrintResolvedMapping(versions);

    var versionArgs = versions
        .SelectMany(kvp => new[] { "--explicit-version", $"{kvp.Key}={kvp.Value}" })
        .ToArray();

    await StepAsync(ctx, results, "PreFlightCheck", ["--target", "PreFlightCheck", .. versionArgs]);
    await StepAsync(ctx, results, "EnsureVcpkgDependencies", ["--target", "EnsureVcpkgDependencies", "--rid", ctx.HostRid]);
    await StepAsync(ctx, results, "Harvest", ["--target", "Harvest", "--rid", ctx.HostRid]);
    await StepAsync(ctx, results, "NativeSmoke", ["--target", "NativeSmoke", "--rid", ctx.HostRid]);
    await StepAsync(ctx, results, "ConsolidateHarvest", ["--target", "ConsolidateHarvest"]);
    await StepAsync(ctx, results, "Package", ["--target", "Package", .. versionArgs]);
    await StepAsync(ctx, results, "PackageConsumerSmoke", ["--target", "PackageConsumerSmoke", "--rid", ctx.HostRid, .. versionArgs]);
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

    if (!verbose)
    {
        // Silent: drain streams to prevent deadlock, no console output, no log file.
        var outTask = process.StandardOutput.ReadToEndAsync();
        var errTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        await Task.WhenAll(outTask, errTask);
        return process.ExitCode;
    }

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

    var stdout = stdoutSb.ToString();
    var stderr = stderrSb.ToString();

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
    body.Append("# exit=").Append(process.ExitCode)
        .Append(' ').Append("finished=").AppendLine(DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));

    await File.WriteAllTextAsync(logPath, body.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    return process.ExitCode;
}

static async Task<Dictionary<string, string>> ParseResolvedVersionsAsync(string repoRoot)
{
    var jsonPath = Path.Combine(repoRoot, "artifacts", "resolve-versions", "versions.json");
    if (!File.Exists(jsonPath))
    {
        throw new InvalidOperationException(
            $"ResolveVersions completed but '{jsonPath}' is missing. Inspect the ResolveVersions log for the emitted path.");
    }

    await using var stream = File.OpenRead(jsonPath);
    using var document = await JsonDocument.ParseAsync(stream);
    var root = document.RootElement;

    // Flat family -> version dict emitted by ResolveVersionsTaskRunner:
    // { "sdl2-core": "2.32.0-local.<ts>", "sdl2-gfx": "...", ... }
    if (root.ValueKind != JsonValueKind.Object)
    {
        throw new InvalidOperationException(
            $"ResolveVersions JSON at '{jsonPath}' is not a JSON object ({root.ValueKind}).");
    }

    var mapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    foreach (var entry in root.EnumerateObject())
    {
        var value = entry.Value.GetString()
            ?? throw new InvalidOperationException($"ResolveVersions JSON carries a null version for family '{entry.Name}'.");
        mapping[entry.Name] = value;
    }

    if (mapping.Count == 0)
    {
        throw new InvalidOperationException($"ResolveVersions JSON at '{jsonPath}' carries an empty mapping.");
    }

    return mapping;
}

static async Task<HashSet<string>> LoadConcreteFamiliesAsync(string repoRoot)
{
    var manifestPath = Path.Combine(repoRoot, "build", "manifest.json");
    if (!File.Exists(manifestPath))
    {
        throw new InvalidOperationException(
            $"Cannot filter concrete families: manifest '{manifestPath}' is missing.");
    }

    await using var stream = File.OpenRead(manifestPath);
    using var document = await JsonDocument.ParseAsync(stream);
    var root = document.RootElement;

    if (!root.TryGetProperty("package_families", out var families) || families.ValueKind != JsonValueKind.Array)
    {
        throw new InvalidOperationException(
            $"Manifest '{manifestPath}' does not expose a 'package_families' array.");
    }

    var concrete = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var family in families.EnumerateArray())
    {
        if (!family.TryGetProperty("name", out var nameElement))
        {
            continue;
        }

        var name = nameElement.GetString();
        if (string.IsNullOrWhiteSpace(name))
        {
            continue;
        }

        var managed = family.TryGetProperty("managed_project", out var m) ? m.GetString() : null;
        var native = family.TryGetProperty("native_project", out var n) ? n.GetString() : null;

        if (!string.IsNullOrWhiteSpace(managed) && !string.IsNullOrWhiteSpace(native))
        {
            concrete.Add(name);
        }
    }

    if (concrete.Count == 0)
    {
        throw new InvalidOperationException(
            $"Manifest '{manifestPath}' carries no concrete package families (managed_project + native_project both set).");
    }

    return concrete;
}

static void PrintHeader(WitnessContext ctx)
{
    AnsiConsole.Write(new FigletText("smoke-witness").Color(Color.Yellow));

    var table = new Table().NoBorder().HideHeaders();
    table.AddColumn(new TableColumn(string.Empty).PadRight(2));
    table.AddColumn(new TableColumn(string.Empty));
    table.AddRow("[grey]Mode[/]", $"[cyan]{Markup.Escape(ctx.Mode == SmokeMode.Local ? "local" : "ci-sim")}[/]");
    table.AddRow("[grey]Platform[/]", $"[cyan]{Markup.Escape(ctx.Platform)}[/] ([cyan]{Markup.Escape(ctx.HostRid)}[/])");
    table.AddRow("[grey]Repo[/]", $"[cyan]{Markup.Escape(ctx.RepoRoot)}[/]");
    table.AddRow("[grey]HEAD[/]", $"[cyan]{Markup.Escape(ctx.HeadSha ?? "unknown")}[/]");
    table.AddRow("[grey]Logs[/]", $"[cyan]{Markup.Escape(ctx.LogDir)}[/]");
    table.AddRow("[grey]Run ID[/]", $"[cyan]{Markup.Escape(ctx.RunId)}[/]");
    table.AddRow("[grey]Verbose[/]", ctx.Verbose ? "[yellow]on (--verbose)[/]" : "[grey]off[/]");
    AnsiConsole.Write(table);
    AnsiConsole.WriteLine();
}

static void PrintResolvedMapping(Dictionary<string, string> versions)
{
    AnsiConsole.MarkupLine("[grey]Resolved mapping (post-ResolveVersions JSON):[/]");
    foreach (var (family, version) in versions.OrderBy(static p => p.Key, StringComparer.OrdinalIgnoreCase))
    {
        AnsiConsole.MarkupLine($"  [cyan]{Markup.Escape(family)}[/]=[yellow]{Markup.Escape(version)}[/]");
    }

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
