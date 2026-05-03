#!/usr/bin/env dotnet
#:property TargetFramework=net10.0
#:property TargetFrameworks=
#:property PublishAot=false
#:property NoError=$(NoError);CA1502;CA1505;CA1031;CA1515;CA2007;CA1812;CA1869;IL2026;IL3050;CA1050;MA0047;S3903;MA0051;S1075;S3267;CA1860;MA0002;MA0015;RCS1214;MA0045;S1144;CA1823;S927;CA1725
#:property NoWarn=$(NoWarn);CA1502;CA1505;CA1031;CA1515;CA2007;CA1812;CA1869;IL2026;IL3050;CA1050;MA0047;S3903;MA0051;S1075;S3267;CA1860;MA0002;MA0015;RCS1214;MA0045;S1144;CA1823;S927;CA1725
#:package Spectre.Console
#:package Spectre.Console.Cli
#:package CliWrap
#:package NuGet.Protocol

using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using CliWrap;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using Spectre.Console;
using Spectre.Console.Cli;

var app = new CommandApp();
app.Configure(config =>
{
    config.Settings.StrictParsing = false; // passthrough: build forwards unknown flags to Cake
    config.SetExceptionHandler((ex, _) =>
    {
        AnsiConsole.WriteException(ex, ExceptionFormats.ShortenPaths);
        return ex switch
        {
            InvalidOperationException => 64,
            FileNotFoundException => 66,
            _ => 1,
        };
    });
    config.AddCommand<BuildCommand>("build");
    config.AddCommand<SetupCommand>("setup");
    config.AddCommand<CiSimCommand>("ci-sim");
});
return await app.RunAsync(args);

// ──────────────────────────────────────────────────────────────────
// Settings
// ──────────────────────────────────────────────────────────────────

public sealed class SetupSettings : CommandSettings
{
    [CommandOption("-s|--source")]
    [Description("Artifact source for local setup: local, remote-github, or remote-nuget.")]
    public string Source { get; init; } = "local";

    [CommandOption("--no-clean")]
    [Description("Skip CleanArtifacts before setup.")]
    public bool NoClean { get; init; }

    public override ValidationResult Validate()
    {
        return string.IsNullOrWhiteSpace(Source)
            ? ValidationResult.Error("--source cannot be empty. Expected: local, remote-github, remote-nuget.")
            : ValidationResult.Success();
    }
}

public sealed class CiSimSettings : CommandSettings
{
    [CommandOption("-v|--verbose")]
    [Description("Tee each Cake step's output to the console while still writing per-step logs.")]
    [DefaultValue(false)]
    public bool Verbose { get; init; }
}

// ReSharper disable once ClassNeverInstantiated.Global — instantiated by Spectre DI
public sealed class BuildSettings : CommandSettings { }

public sealed class BuildCommand : AsyncCommand<BuildSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, BuildSettings settings)
    {
        var remainingRaw = context.Remaining.Raw.ToArray();
        var cakeArgs = remainingRaw.Length > 0
            ? remainingRaw
            : [.. context.Arguments.Skip(1)];
        return await Shared.RunCakePassthroughAsync(cakeArgs);
    }
}

// ──────────────────────────────────────────────────────────────────
// Commands
// ──────────────────────────────────────────────────────────────────

public sealed class SetupCommand : AsyncCommand<SetupSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext ctx, SetupSettings settings)
    {
        var source = settings.Source.Trim().ToLowerInvariant();
        return source switch
        {
            "local" => await RunLocalAsync(settings.NoClean),
            "remote-github" => await RunRemoteGitHubAsync(settings.NoClean),
            "remote-nuget" => RunRemoteNuGetStub(),
            _ => FailUnknownSource(source),
        };
    }

    private static int FailUnknownSource(string source)
    {
        AnsiConsole.MarkupLine($"[red]Unknown --source value '{Markup.Escape(source)}'. Expected: local, remote-github, remote-nuget.[/]");
        return 64;
    }

    private static int RunRemoteNuGetStub()
    {
        AnsiConsole.MarkupLine("[yellow]remote-nuget source not yet implemented (Phase 2b PD-7 territory).[/]");
        return 64;
    }

    private static async Task<int> RunLocalAsync(bool noClean)
    {
        var repoRoot = await Shared.ResolveRepoRootAsync();
        var hostRid = Shared.ResolveHostRid();
        var families = Shared.GetConcreteFamilies(repoRoot);
        var suffix = Shared.LocalSuffix();
        var logDir = Shared.CreateLogDir(repoRoot, "setup-local");

        PrintHeader("setup --source=local", repoRoot, hostRid, logDir);

        var totalStopwatch = Stopwatch.StartNew();
        var results = new List<Shared.StepResult>();

        if (!noClean)
        {
            if (!await Shared.RunCakeStepWithLogAsync(results, repoRoot, logDir, "CleanArtifacts",
                    ["--target", "CleanArtifacts"], verbose: false))
                return FinishSetup(results, totalStopwatch, 1);
        }
        else
        {
            AnsiConsole.MarkupLine("[grey]Skipping CleanArtifacts (--no-clean).[/]");
        }

        var scopeArgs = families.SelectMany(f => new[] { "--scope", f.Name }).ToArray();
        var resolveArgs = new List<string> { "--target", "ResolveVersions", "--version-source=manifest", $"--suffix={suffix}" };
        resolveArgs.AddRange(scopeArgs);
        if (!await Shared.RunCakeStepWithLogAsync(results, repoRoot, logDir, "ResolveVersions", resolveArgs, verbose: false))
            return FinishSetup(results, totalStopwatch, 1);

        var versionsPath = Path.Combine(repoRoot, "artifacts", "resolve-versions", "versions.json");

        if (!await Shared.RunCakeStepWithLogAsync(results, repoRoot, logDir, "PreFlightCheck",
                ["--target", "PreFlightCheck", "--versions-file", versionsPath], verbose: false))
            return FinishSetup(results, totalStopwatch, 1);

        if (!await Shared.RunCakeStepWithLogAsync(results, repoRoot, logDir, "EnsureVcpkgDependencies",
                ["--target", "EnsureVcpkgDependencies", "--rid", hostRid], verbose: false))
            return FinishSetup(results, totalStopwatch, 1);

        if (!await Shared.RunCakeStepWithLogAsync(results, repoRoot, logDir, "Harvest",
                ["--target", "Harvest", "--rid", hostRid], verbose: false))
            return FinishSetup(results, totalStopwatch, 1);

        if (!await Shared.RunCakeStepWithLogAsync(results, repoRoot, logDir, "ConsolidateHarvest",
                ["--target", "ConsolidateHarvest"], verbose: false))
            return FinishSetup(results, totalStopwatch, 1);

        if (!await Shared.RunCakeStepWithLogAsync(results, repoRoot, logDir, "Package",
                ["--target", "Package", "--versions-file", versionsPath], verbose: false))
            return FinishSetup(results, totalStopwatch, 1);

        AnsiConsole.MarkupLine("[grey]Verifying nupkg outputs...[/]");
        var packagesDir = Path.Combine(repoRoot, "artifacts", "packages");
        var versions = await Shared.ReadVersionsFromJsonAsync(versionsPath);
        var missingPackages = new List<string>();
        foreach (var family in families)
        {
            if (!versions.TryGetValue(family.Name, out var version))
            {
                missingPackages.Add($"{family.Name}: missing version mapping in versions.json");
                AnsiConsole.MarkupLine($"  [red]✗[/] [cyan]{Markup.Escape(family.Name)}[/] — missing version mapping");
                continue;
            }

            var expectedPackages = Shared.ExpectedNupkgPaths(packagesDir, family.Name, version);
            var found = expectedPackages.All(File.Exists);
            AnsiConsole.MarkupLine(found
                ? $"  [green]✓[/] [cyan]{Markup.Escape(family.Name)}[/] [grey]{Markup.Escape(version)}[/]"
                : $"  [red]✗[/] [cyan]{Markup.Escape(family.Name)}[/] [grey]{Markup.Escape(version)}[/] — exact nupkg missing");

            missingPackages.AddRange(expectedPackages
                .Where(path => !File.Exists(path))
                .Select(path => Path.GetFileName(path) ?? path));
        }

        if (missingPackages.Count > 0)
        {
            AnsiConsole.MarkupLine("[red]Some exact nupkgs are missing — stale packages will not be accepted.[/]");
            foreach (var missing in missingPackages)
                AnsiConsole.MarkupLine($"  [red]missing[/] [grey]{Markup.Escape(missing)}[/]");
            return FinishSetup(results, totalStopwatch, 1);
        }

        Shared.WriteLocalProps(repoRoot, packagesDir, versions);

        AnsiConsole.MarkupLine($"[green]Janset.Local.props written to[/] [cyan]build/msbuild/Janset.Local.props[/]");
        AnsiConsole.MarkupLine($"[green]versions.json produced by ResolveVersions at[/] [cyan]artifacts/resolve-versions/versions.json[/]");

        return FinishSetup(results, totalStopwatch, 0);
    }

    private static async Task<int> RunRemoteGitHubAsync(bool noClean)
    {
        var repoRoot = await Shared.ResolveRepoRootAsync();
        var hostRid = Shared.ResolveHostRid();
        var families = Shared.GetConcreteFamilies(repoRoot);
        var logDir = Shared.CreateLogDir(repoRoot, "setup-remote-github");

        PrintHeader("setup --source=remote-github", repoRoot, hostRid, logDir);

        var authToken = Shared.ResolveGitHubToken();
        if (authToken is null)
        {
            AnsiConsole.MarkupLine("[red]GH_TOKEN or GITHUB_TOKEN env var required for --source=remote-github.[/]");
            AnsiConsole.MarkupLine("[grey]Create a Classic PAT with read:packages scope. Fine-grained PATs are unsupported by GH Packages NuGet.[/]");
            return 1;
        }

        var totalStopwatch = Stopwatch.StartNew();
        var results = new List<Shared.StepResult>();

        if (!noClean)
        {
            if (!await Shared.RunCakeStepWithLogAsync(results, repoRoot, logDir, "CleanArtifacts",
                    ["--target", "CleanArtifacts"], verbose: false))
                return FinishSetup(results, totalStopwatch, 1);
        }
        else
        {
            AnsiConsole.MarkupLine("[grey]Skipping CleanArtifacts (--no-clean).[/]");
        }

        var packagesDir = Path.Combine(repoRoot, "artifacts", "packages");
        var acquisition = await RunRemoteGitHubAcquisitionStepAsync(results, logDir, families, packagesDir, authToken);
        if (!acquisition.Succeeded)
            return FinishSetup(results, totalStopwatch, 1);

        var versions = acquisition.Resolved.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToNormalizedString(), StringComparer.OrdinalIgnoreCase);
        Shared.WriteLocalProps(repoRoot, packagesDir, versions);
        Shared.WriteVersionsJson(repoRoot, versions);

        AnsiConsole.MarkupLine($"[green]{acquisition.Resolved.Count} families pulled from GH Packages into[/] [cyan]artifacts/packages/[/]");

        return FinishSetup(results, totalStopwatch, 0);
    }

    private static async Task<(bool Succeeded, SortedDictionary<string, NuGetVersion> Resolved)> RunRemoteGitHubAcquisitionStepAsync(
        List<Shared.StepResult> results,
        string logDir,
        IReadOnlyList<Shared.PackageFamilyConfig> families,
        string packagesDir,
        string authToken)
    {
        var stepNumber = results.Count + 1;
        var label = "RemoteGitHubFeed";
        var logPath = Shared.BuildStepLogPath(logDir, stepNumber, label);
        var resolved = new SortedDictionary<string, NuGetVersion>(StringComparer.OrdinalIgnoreCase);

        AnsiConsole.Write(new Rule($"[yellow]{stepNumber:00}. {Markup.Escape(label)}[/]").RuleStyle("grey").LeftJustified());
        AnsiConsole.MarkupLine($"[grey]feed:[/] [cyan]{Markup.Escape(Shared.GitHubPackagesFeedUrl)}[/]");

        var stopwatch = Stopwatch.StartNew();
        var exitCode = 0;

        await using var logStream = new FileStream(logPath, FileMode.Create, FileAccess.Write, FileShare.Read);
        await using var logWriter = new StreamWriter(logStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        await logWriter.WriteLineAsync($"# feed={Shared.GitHubPackagesFeedUrl}");
        await logWriter.WriteLineAsync($"# packages={packagesDir}");
        await logWriter.WriteLineAsync();

        try
        {
            Shared.WipeJansetPackages(packagesDir, message => logWriter.WriteLine(message));

            foreach (var family in families)
            {
                var managedId = Shared.ManagedPackageId(family.Name);
                var nativeId = Shared.NativePackageId(family.Name);

                var managedVersion = await GetLatestRemoteVersionAsync(authToken, family.Name, managedId, logWriter);
                var nativeVersion = await GetLatestRemoteVersionAsync(authToken, family.Name, nativeId, logWriter);

                if (managedVersion is null)
                {
                    throw new RemoteGitHubSetupException(
                        $"Family '{family.Name}' has no published managed package '{managedId}' on GitHub Packages. First publish wave has not shipped yet, or this package was not pushed. Run local setup or publish a coherent staging wave.");
                }

                if (nativeVersion is null)
                {
                    throw new RemoteGitHubSetupException(
                        $"Family '{family.Name}' has no published native package '{nativeId}' on GitHub Packages while managed '{managedId}' is at {managedVersion.ToNormalizedString()}. This looks like a partial publish; re-run the failing publish wave or use local setup.");
                }

                if (!VersionComparer.Default.Equals(managedVersion, nativeVersion))
                {
                    throw new RemoteGitHubSetupException(
                        $"Family '{family.Name}' managed/native versions disagree: {managedId}@{managedVersion.ToNormalizedString()} vs {nativeId}@{nativeVersion.ToNormalizedString()}. This is likely a partial publish; repair the internal feed or supply a coherent version set in a future explicit-version flow.");
                }

                AnsiConsole.MarkupLine($"  [green]✓[/] [cyan]{Markup.Escape(family.Name)}[/] = [yellow]{managedVersion.ToNormalizedString()}[/]");
                await logWriter.WriteLineAsync($"{family.Name}={managedVersion.ToNormalizedString()}");

                await DownloadRemotePackageAsync(authToken, managedId, managedVersion, packagesDir, logWriter);
                await DownloadRemotePackageAsync(authToken, nativeId, nativeVersion, packagesDir, logWriter);

                resolved[family.Name] = managedVersion;
            }
        }
        catch (RemoteGitHubSetupException ex)
        {
            exitCode = 1;
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
            await logWriter.WriteLineAsync();
            await logWriter.WriteLineAsync(ex.ToString());
        }
        catch (Exception ex)
        {
            exitCode = 1;
            var message = Shared.DescribeNuGetFailure(ex, "GitHub Packages", "query/download");
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(message)}[/]");
            await logWriter.WriteLineAsync();
            await logWriter.WriteLineAsync(ex.ToString());
        }

        stopwatch.Stop();
        await logWriter.WriteLineAsync();
        await logWriter.WriteLineAsync($"# exit={exitCode} finished={DateTimeOffset.UtcNow:O}");
        await logWriter.FlushAsync();

        results.Add(new Shared.StepResult(label, exitCode, stopwatch.Elapsed, logPath));
        if (exitCode == 0)
            AnsiConsole.MarkupLine($"[green]OK[/]   ({stopwatch.Elapsed.TotalSeconds:F1}s) → [grey]{Markup.Escape(logPath)}[/]");
        else
            AnsiConsole.MarkupLine($"[red]FAIL[/] ({stopwatch.Elapsed.TotalSeconds:F1}s) → [cyan]{Markup.Escape(logPath)}[/]");
        AnsiConsole.WriteLine();

        return (exitCode == 0, resolved);
    }

    private static async Task<NuGetVersion?> GetLatestRemoteVersionAsync(
        string authToken,
        string familyName,
        string packageId,
        TextWriter logWriter)
    {
        await logWriter.WriteLineAsync($"discover {familyName} {packageId}");
        try
        {
            return await Shared.GetLatestNuGetVersionAsync(Shared.GitHubPackagesFeedUrl, authToken, packageId);
        }
        catch (Exception ex)
        {
            throw new RemoteGitHubSetupException(Shared.DescribeNuGetFailure(ex, packageId, "query"), ex);
        }
    }

    private static async Task DownloadRemotePackageAsync(
        string authToken,
        string packageId,
        NuGetVersion version,
        string packagesDir,
        TextWriter logWriter)
    {
        await logWriter.WriteLineAsync($"download {packageId} {version.ToNormalizedString()}");
        try
        {
            await Shared.DownloadNuGetPackageAsync(Shared.GitHubPackagesFeedUrl, authToken, packageId, version, packagesDir);
        }
        catch (Exception ex)
        {
            throw new RemoteGitHubSetupException(Shared.DescribeNuGetFailure(ex, packageId, "download"), ex);
        }
    }

    private static int FinishSetup(List<Shared.StepResult> results, Stopwatch totalStopwatch, int exitCode)
    {
        totalStopwatch.Stop();
        Shared.PrintSummaryTable(results, totalStopwatch.Elapsed);
        if (exitCode != 0 && results.LastOrDefault()?.LogPath is { } logPath)
            AnsiConsole.MarkupLine($"[yellow]Inspect failing log:[/] [cyan]{Markup.Escape(logPath)}[/]");
        return exitCode;
    }

    private static void PrintHeader(string label, string repoRoot, string hostRid, string? logDir = null)
    {
        AnsiConsole.Write(new FigletText("tools").Color(Color.Yellow));
        AnsiConsole.MarkupLine($"[grey]Command:[/] [cyan]{Markup.Escape(label)}[/]");
        AnsiConsole.MarkupLine($"[grey]Host RID:[/] [cyan]{Markup.Escape(hostRid)}[/]");
        AnsiConsole.MarkupLine($"[grey]Repo:[/] [cyan]{Markup.Escape(repoRoot)}[/]");
        if (logDir is not null)
            AnsiConsole.MarkupLine($"[grey]Logs:[/] [cyan]{Markup.Escape(logDir)}[/]");
        AnsiConsole.WriteLine();
    }

}

public sealed class RemoteGitHubSetupException : Exception
{
    public RemoteGitHubSetupException()
    {
    }

    public RemoteGitHubSetupException(string message)
        : base(message)
    {
    }

    public RemoteGitHubSetupException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class CiSimCommand : AsyncCommand<CiSimSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext ctx, CiSimSettings settings)
    {
        var repoRoot = await Shared.ResolveRepoRootAsync();
        var hostRid = Shared.ResolveHostRid();
        var families = Shared.GetConcreteFamilies(repoRoot);
        var suffix = Shared.LocalSuffix();
        var platform = Shared.ResolvePlatformTag();
        var runId = DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);
        var logDir = Path.Combine(repoRoot, ".logs", "tools", $"{platform}-ci-sim-{runId}");

        if (Directory.Exists(logDir))
            Directory.Delete(logDir, recursive: true);
        Directory.CreateDirectory(logDir);

        AnsiConsole.Write(new FigletText("tools").Color(Color.Yellow));
        AnsiConsole.MarkupLine($"[grey]Command:[/] [cyan]ci-sim[/]");
        AnsiConsole.MarkupLine($"[grey]Host RID:[/] [cyan]{Markup.Escape(hostRid)}[/]");
        AnsiConsole.MarkupLine($"[grey]Repo:[/] [cyan]{Markup.Escape(repoRoot)}[/]");
        AnsiConsole.MarkupLine($"[grey]Logs:[/] [cyan]{Markup.Escape(logDir)}[/]");
        AnsiConsole.MarkupLine($"[grey]Verbose:[/] {(settings.Verbose ? "[yellow]on[/]" : "[grey]off[/]")}");
        AnsiConsole.WriteLine();

        var totalStopwatch = Stopwatch.StartNew();
        var results = new List<Shared.StepResult>();

        var scopeArgs = families.SelectMany(f => new[] { "--scope", f.Name }).ToArray();

        // Step 1: CleanArtifacts
        if (!await Shared.RunCakeStepWithLogAsync(results, repoRoot, logDir, "CleanArtifacts",
                ["--target", "CleanArtifacts"], settings.Verbose))
            return FinishCiSim(results, totalStopwatch.Elapsed, logDir, 1);

        // Step 2: ResolveVersions
        var resolveArgs = new List<string> { "--target", "ResolveVersions", "--version-source=manifest", $"--suffix={suffix}" };
        resolveArgs.AddRange(scopeArgs);
        if (!await Shared.RunCakeStepWithLogAsync(results, repoRoot, logDir, "ResolveVersions",
                resolveArgs, settings.Verbose))
            return FinishCiSim(results, totalStopwatch.Elapsed, logDir, 1);

        var versionsPath = Path.Combine(repoRoot, "artifacts", "resolve-versions", "versions.json");

        // Step 3: PreFlightCheck
        if (!await Shared.RunCakeStepWithLogAsync(results, repoRoot, logDir, "PreFlightCheck",
                ["--target", "PreFlightCheck", "--versions-file", versionsPath], settings.Verbose))
            return FinishCiSim(results, totalStopwatch.Elapsed, logDir, 1);

        // Step 4: EnsureVcpkgDependencies
        if (!await Shared.RunCakeStepWithLogAsync(results, repoRoot, logDir, "EnsureVcpkgDependencies",
                ["--target", "EnsureVcpkgDependencies", "--rid", hostRid], settings.Verbose))
            return FinishCiSim(results, totalStopwatch.Elapsed, logDir, 1);

        // Step 5: Harvest
        if (!await Shared.RunCakeStepWithLogAsync(results, repoRoot, logDir, "Harvest",
                ["--target", "Harvest", "--rid", hostRid], settings.Verbose))
            return FinishCiSim(results, totalStopwatch.Elapsed, logDir, 1);

        // Step 6: NativeSmoke
        if (!await Shared.RunCakeStepWithLogAsync(results, repoRoot, logDir, "NativeSmoke",
                ["--target", "NativeSmoke", "--rid", hostRid], settings.Verbose))
            return FinishCiSim(results, totalStopwatch.Elapsed, logDir, 1);

        // Step 7: ConsolidateHarvest
        if (!await Shared.RunCakeStepWithLogAsync(results, repoRoot, logDir, "ConsolidateHarvest",
                ["--target", "ConsolidateHarvest"], settings.Verbose))
            return FinishCiSim(results, totalStopwatch.Elapsed, logDir, 1);

        // Step 8: Package
        if (!await Shared.RunCakeStepWithLogAsync(results, repoRoot, logDir, "Package",
                ["--target", "Package", "--versions-file", versionsPath], settings.Verbose))
            return FinishCiSim(results, totalStopwatch.Elapsed, logDir, 1);

        // Step 9: PackageConsumerSmoke
        if (!await Shared.RunCakeStepWithLogAsync(results, repoRoot, logDir, "PackageConsumerSmoke",
                ["--target", "PackageConsumerSmoke", "--rid", hostRid, "--versions-file", versionsPath], settings.Verbose))
            return FinishCiSim(results, totalStopwatch.Elapsed, logDir, 1);

        // Refresh Janset.Local.props so IDE-driven smoke restore stays in sync with the
        // freshly-packed nupkgs. ci-sim's CleanArtifacts step wipes the previous setup
        // run's packages, so without this rewrite the props would dangle at stale versions.
        var packagesDir = Path.Combine(repoRoot, "artifacts", "packages");
        var versions = await Shared.ReadVersionsFromJsonAsync(versionsPath);
        Shared.WriteLocalProps(repoRoot, packagesDir, versions);
        AnsiConsole.MarkupLine($"[green]Janset.Local.props refreshed at[/] [cyan]build/msbuild/Janset.Local.props[/]");

        return FinishCiSim(results, totalStopwatch.Elapsed, logDir, 0);
    }

    private static int FinishCiSim(List<Shared.StepResult> results, TimeSpan totalDuration, string logDir, int exitCode)
    {
        Shared.PrintSummaryTable(results, totalDuration);
        if (exitCode != 0)
            AnsiConsole.MarkupLine($"[yellow]Inspect failing logs under[/] [cyan]{Markup.Escape(logDir)}[/].");
        return exitCode;
    }
}

// ──────────────────────────────────────────────────────────────────
// Shared helpers
// ──────────────────────────────────────────────────────────────────

internal static class Shared
{
    // ── path / identity helpers ──

    /// <summary>Pure Cake passthrough — forwards output directly to the console, no log capture.</summary>
    public static async Task<int> RunCakePassthroughAsync(string[] cakeArgs)
    {
        var repoRoot = await ResolveRepoRootAsync();
        var fullArgs = BuildCakeArgList(repoRoot, cakeArgs);
        var displayCommand = "dotnet " + string.Join(' ', fullArgs.Select(QuoteArg));
        AnsiConsole.MarkupLine($"[grey]cmd:[/] [cyan]{Markup.Escape(displayCommand)}[/]");

        var result = await Cli.Wrap("dotnet")
            .WithArguments(fullArgs)
            .WithWorkingDirectory(repoRoot)
            .WithStandardOutputPipe(PipeTarget.ToDelegate(line => Console.Out.WriteLine(line)))
            .WithStandardErrorPipe(PipeTarget.ToDelegate(line => Console.Error.WriteLine(line)))
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync();

        return result.ExitCode;
    }

    public static async Task<string> ResolveRepoRootAsync()
    {
        var stdOut = new StringBuilder();
        var result = await Cli.Wrap("git")
            .WithArguments(["rev-parse", "--show-toplevel"])
            .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdOut))
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync();

        var output = stdOut.ToString().Trim();
        if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
            throw new InvalidOperationException("Could not resolve repository root via git. Run from inside the repo.");

        return output;
    }

    public static string ResolveHostRid()
    {
        string os;
        if (OperatingSystem.IsWindows()) os = "win";
        else if (OperatingSystem.IsLinux()) os = "linux";
        else if (OperatingSystem.IsMacOS()) os = "osx";
        else throw new PlatformNotSupportedException("Unknown OS for RID derivation.");

        var arch = RuntimeInformation.OSArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            Architecture.X86 => "x86",
            Architecture.Arm => "arm",
            var other => throw new PlatformNotSupportedException($"Unknown architecture '{other}'."),
        };
        return $"{os}-{arch}";
    }

    public static string ResolvePlatformTag()
    {
        if (OperatingSystem.IsWindows()) return "windows";
        if (OperatingSystem.IsLinux()) return "linux";
        if (OperatingSystem.IsMacOS()) return "macos";
        return "unknown";
    }

    public static string LocalSuffix()
        => string.Create(CultureInfo.InvariantCulture,
            $"local.{DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmss", CultureInfo.InvariantCulture)}");

    public static string CreateLogDir(string repoRoot, string subcommandTag)
    {
        var platform = ResolvePlatformTag();
        var runId = DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);
        var logDir = Path.Combine(repoRoot, ".logs", "tools", $"{platform}-{subcommandTag}-{runId}");

        if (Directory.Exists(logDir))
            Directory.Delete(logDir, recursive: true);

        Directory.CreateDirectory(logDir);
        return logDir;
    }

    // ── manifest reading ──

    public sealed record PackageFamilyConfig(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("managed_project")] string? ManagedProject,
        [property: JsonPropertyName("native_project")] string? NativeProject,
        [property: JsonPropertyName("library_ref")] string LibraryRef);

    public sealed record ManifestConfig(
        [property: JsonPropertyName("package_families")] List<PackageFamilyConfig> PackageFamilies);

    public static List<PackageFamilyConfig> GetConcreteFamilies(string repoRoot)
    {
        var manifestPath = Path.Combine(repoRoot, "build", "manifest.json");
        if (!File.Exists(manifestPath))
            throw new InvalidOperationException($"manifest.json not found at '{manifestPath}'.");

        var json = File.ReadAllText(manifestPath);
        var manifest = JsonSerializer.Deserialize<ManifestConfig>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        }) ?? throw new InvalidOperationException("Failed to deserialize manifest.json.");

        var families = manifest.PackageFamilies
            .Where(f => !string.IsNullOrWhiteSpace(f.ManagedProject) && !string.IsNullOrWhiteSpace(f.NativeProject))
            .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (families.Count == 0)
            throw new InvalidOperationException("No concrete families found in manifest.json (need both managed_project and native_project).");

        return families;
    }

    // ── family naming (inline replica of FamilyIdentifierConventions) ──

    public static string ManagedPackageId(string familyIdentifier)
    {
        var (major, role) = ParseFamily(familyIdentifier);
        return $"Janset.SDL{major}.{role}";
    }

    public static string NativePackageId(string familyIdentifier)
    {
        var (major, role) = ParseFamily(familyIdentifier);
        return $"Janset.SDL{major}.{role}.Native";
    }

    public static string VersionPropertyName(string familyIdentifier)
    {
        var (major, role) = ParseFamily(familyIdentifier);
        return $"JansetSdl{major}{role}PackageVersion";
    }

    private static (string Major, string Role) ParseFamily(string familyIdentifier)
    {
        var dashIndex = familyIdentifier.IndexOf('-', StringComparison.Ordinal);
        if (dashIndex <= 0 || dashIndex >= familyIdentifier.Length - 1)
            throw new ArgumentException($"Family identifier must follow 'sdl<major>-<role>' format. Got: '{familyIdentifier}'.");

        var sdlPart = familyIdentifier[..dashIndex];
        var role = familyIdentifier[(dashIndex + 1)..];

        if (!sdlPart.StartsWith("sdl", StringComparison.OrdinalIgnoreCase) || sdlPart.Length <= 3)
            throw new ArgumentException($"Family identifier prefix must be 'sdl<major>'. Got: '{sdlPart}'.");

        var major = sdlPart[3..];
        if (!major.All(char.IsDigit))
            throw new ArgumentException($"SDL major must be all digits. Got: '{major}'.");

        return (major, ToPascalCase(role));
    }

    private static string ToPascalCase(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return string.Create(value.Length, value, static (span, source) =>
        {
            source.AsSpan().CopyTo(span);
            span[0] = char.ToUpperInvariant(span[0]);
            for (var i = 1; i < span.Length; i++)
                span[i] = char.ToLowerInvariant(span[i]);
        });
    }

    // ── Janset.Local.props writing ──

    public static void WriteLocalProps(string repoRoot, string feedPath, IReadOnlyDictionary<string, string> versions)
    {
        var propertyGroup = new XElement("PropertyGroup",
            new XElement("LocalPackageFeed", feedPath));

        foreach (var pair in versions.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
        {
            var propName = VersionPropertyName(pair.Key);
            propertyGroup.Add(new XElement(propName, pair.Value));
        }

        var document = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("Project", propertyGroup));

        var targetPath = Path.Combine(repoRoot, "build", "msbuild", "Janset.Local.props");
        var targetDir = Path.GetDirectoryName(targetPath)!;
        if (!Directory.Exists(targetDir))
            Directory.CreateDirectory(targetDir);

        File.WriteAllText(targetPath, string.Concat(document.Declaration?.ToString(), "\n", document.ToString()),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    // ── versions.json writing / reading ──

    public static void WriteVersionsJson(string repoRoot, IReadOnlyDictionary<string, string> versions)
    {
        var sorted = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in versions)
            sorted[key] = value;
        var targetDir = Path.Combine(repoRoot, "artifacts", "resolve-versions");
        if (!Directory.Exists(targetDir))
            Directory.CreateDirectory(targetDir);

        var targetPath = Path.Combine(targetDir, "versions.json");
        var json = JsonSerializer.Serialize(sorted, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(targetPath, json + Environment.NewLine, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    public static async Task<IReadOnlyDictionary<string, string>> ReadVersionsFromJsonAsync(string path)
    {
        var json = await File.ReadAllTextAsync(path);
        var versions = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                       ?? new Dictionary<string, string>();
        return new SortedDictionary<string, string>(versions, StringComparer.OrdinalIgnoreCase);
    }

    public static string[] ExpectedNupkgPaths(string packagesDir, string familyName, string version)
    {
        return
        [
            Path.Combine(packagesDir, $"{ManagedPackageId(familyName)}.{version}.nupkg"),
            Path.Combine(packagesDir, $"{NativePackageId(familyName)}.{version}.nupkg"),
        ];
    }

    public static void WipeJansetPackages(string packagesDir, Action<string> log)
    {
        ArgumentNullException.ThrowIfNull(log);

        if (!Directory.Exists(packagesDir))
        {
            Directory.CreateDirectory(packagesDir);
            log($"created {packagesDir}");
            return;
        }

        var patterns = new[] { "Janset.SDL2.*", "Janset.SDL3.*" };
        foreach (var path in patterns.SelectMany(pattern => Directory.GetFiles(packagesDir, pattern, SearchOption.TopDirectoryOnly)))
        {
            File.Delete(path);
            log($"deleted stale package {path}");
        }
    }

    // ── Cake process execution (CliWrap) ──

    public sealed record StepResult(string Label, int ExitCode, TimeSpan Duration, string? LogPath = null)
    {
        public bool Succeeded => ExitCode == 0;
    }

    /// <summary>Run a Cake target without per-step log persistence (used by setup).</summary>
    public static async Task<bool> RunCakeStepAsync(
        List<StepResult> results, string repoRoot, string label, IReadOnlyList<string> cakeArgs, bool verbose)
    {
        var stepNumber = results.Count + 1;
        AnsiConsole.Write(new Rule($"[yellow]{stepNumber:00}. {Markup.Escape(label)}[/]").RuleStyle("grey").LeftJustified());

        var fullArgs = BuildCakeArgList(repoRoot, cakeArgs);
        var displayCommand = "dotnet " + string.Join(' ', fullArgs.Select(QuoteArg));
        AnsiConsole.MarkupLine($"[grey]cmd:[/] [cyan]{Markup.Escape(displayCommand)}[/]");

        var stopwatch = Stopwatch.StartNew();
        CommandResult result;
        if (verbose)
        {
            // Per-step log under a run-specific subdir so concurrent setup invocations
            // never overwrite each other's log files.
            var runId = DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);
            var logPath = Path.Combine(repoRoot, ".logs", "tools", $"setup-{runId}", $"{stepNumber:00}-{SanitizeForFile(label)}.log");
            var logDir = Path.GetDirectoryName(logPath)!;
            if (!Directory.Exists(logDir)) Directory.CreateDirectory(logDir);

            result = await Cli.Wrap("dotnet")
                .WithArguments(fullArgs)
                .WithWorkingDirectory(repoRoot)
                .WithStandardOutputPipe(CreateLineLogPipe(logPath, Console.Out))
                .WithStandardErrorPipe(CreateLineLogPipe(logPath, Console.Error))
                .WithValidation(CommandResultValidation.None)
                .ExecuteAsync();
        }
        else
        {
            // Explicit null-stream pipes prevent the child process from deadlocking
            // when it writes enough stdout/stderr to fill the OS pipe buffer.
            // CliWrap's default PipeTarget.Null does not open the stream at all,
            // which is functionally equivalent for most processes but risky for
            // high-output Cake targets. See https://github.com/Tyrrrz/CliWrap/issues/145.
            result = await Cli.Wrap("dotnet")
                .WithArguments(fullArgs)
                .WithWorkingDirectory(repoRoot)
                .WithStandardOutputPipe(PipeTarget.ToStream(Stream.Null))
                .WithStandardErrorPipe(PipeTarget.ToStream(Stream.Null))
                .WithValidation(CommandResultValidation.None)
                .ExecuteAsync();
        }

        stopwatch.Stop();
        var stepResult = new StepResult(label, result.ExitCode, stopwatch.Elapsed);
        results.Add(stepResult);

        if (stepResult.Succeeded)
            AnsiConsole.MarkupLine($"[green]OK[/]   ({stopwatch.Elapsed.TotalSeconds:F1}s)");
        else
            AnsiConsole.MarkupLine($"[red]FAIL[/] ({stopwatch.Elapsed.TotalSeconds:F1}s)");
        AnsiConsole.WriteLine();

        return stepResult.Succeeded;
    }

    /// <summary>Run a Cake target WITH per-step log. Returns true if step passed.</summary>
    public static async Task<bool> RunCakeStepWithLogAsync(
        List<StepResult> results, string repoRoot, string logDir, string label, IReadOnlyList<string> cakeArgs, bool verbose)
    {
        var stepNumber = results.Count + 1;
        var logPath = Path.Combine(logDir, $"{stepNumber:00}-{SanitizeForFile(label)}.log");

        AnsiConsole.Write(new Rule($"[yellow]{stepNumber:00}. {Markup.Escape(label)}[/]").RuleStyle("grey").LeftJustified());

        var fullArgs = BuildCakeArgList(repoRoot, cakeArgs);
        var displayCommand = "dotnet " + string.Join(' ', fullArgs.Select(QuoteArg));
        AnsiConsole.MarkupLine($"[grey]cmd:[/] [cyan]{Markup.Escape(displayCommand)}[/]");

        var stopwatch = Stopwatch.StartNew();
        var result = await Cli.Wrap("dotnet")
            .WithArguments(fullArgs)
            .WithWorkingDirectory(repoRoot)
            .WithStandardOutputPipe(CreateLineLogPipe(logPath, verbose ? Console.Out : null))
            .WithStandardErrorPipe(CreateLineLogPipe(logPath, verbose ? Console.Error : null))
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync();
        stopwatch.Stop();

        var stepResult = new StepResult(label, result.ExitCode, stopwatch.Elapsed, logPath);
        results.Add(stepResult);

        if (stepResult.Succeeded)
            AnsiConsole.MarkupLine($"[green]OK[/]   ({stopwatch.Elapsed.TotalSeconds:F1}s) → [grey]{Markup.Escape(logPath)}[/]");
        else
            AnsiConsole.MarkupLine($"[red]FAIL[/] ({stopwatch.Elapsed.TotalSeconds:F1}s) → [cyan]{Markup.Escape(logPath)}[/]");
        AnsiConsole.WriteLine();

        return stepResult.Succeeded;
    }

    private static List<string> BuildCakeArgList(string repoRoot, IReadOnlyList<string> cakeArgs)
    {
        var fullArgs = new List<string>(7 + cakeArgs.Count)
        {
            "run",
            "--project", Path.Combine(repoRoot, "build", "_build", "Build.csproj"),
            "--configuration", "Release",
            "--",
        };
        fullArgs.AddRange(cakeArgs);
        return fullArgs;
    }

    // ── NuGet protocol helpers (remote-github source) ──

    public const string GitHubPackagesFeedUrl = "https://nuget.pkg.github.com/janset2d/index.json";

    public static string? ResolveGitHubToken()
    {
        foreach (var envVar in new[] { "GH_TOKEN", "GITHUB_TOKEN" })
        {
            var value = Environment.GetEnvironmentVariable(envVar);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }
        return null;
    }

    public static async Task<NuGetVersion?> GetLatestNuGetVersionAsync(string feedUrl, string authToken, string packageId)
    {
        var repository = CreateNuGetRepository(feedUrl, authToken);
        var resource = await repository.GetResourceAsync<FindPackageByIdResource>(CancellationToken.None);

        using var cache = new SourceCacheContext { NoCache = true };
        var versions = await resource.GetAllVersionsAsync(packageId, cache, NullLogger.Instance, CancellationToken.None);

        return versions
            .OrderByDescending(v => v, VersionComparer.Default)
            .FirstOrDefault();
    }

    public static async Task DownloadNuGetPackageAsync(
        string feedUrl, string authToken, string packageId, NuGetVersion version, string targetDir)
    {
        var repository = CreateNuGetRepository(feedUrl, authToken);
        var resource = await repository.GetResourceAsync<FindPackageByIdResource>(CancellationToken.None);

        using var cache = new SourceCacheContext { NoCache = true };

        var fileName = $"{packageId}.{version.ToNormalizedString()}.nupkg";
        var targetPath = Path.Combine(targetDir, fileName);

        if (!Directory.Exists(targetDir))
            Directory.CreateDirectory(targetDir);

        await using var fileStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);
        var copied = await resource.CopyNupkgToStreamAsync(packageId, version, fileStream, cache, NullLogger.Instance, CancellationToken.None);

        if (!copied)
            throw new InvalidOperationException(
                $"NuGet feed at '{feedUrl}' could not stream '{packageId}' {version.ToNormalizedString()}. Verify the package was published and the auth token has read:packages scope.");
    }

    private static SourceRepository CreateNuGetRepository(string feedUrl, string authToken)
    {
        var packageSource = new PackageSource(feedUrl)
        {
            Credentials = new PackageSourceCredential(
                source: feedUrl,
                username: "anonymous",
                passwordText: authToken,
                isPasswordClearText: true,
                validAuthenticationTypesText: null),
        };
        return Repository.Factory.GetCoreV3(packageSource);
    }

    public static string DescribeNuGetFailure(Exception exception, string packageId, string operation)
    {
        var exceptions = FlattenExceptions(exception).ToList();
        var message = string.Join(" | ", exceptions.Select(static ex => ex.Message));
        var lower = message.ToLowerInvariant();

        if (lower.Contains("401", StringComparison.Ordinal) || lower.Contains("unauthorized", StringComparison.Ordinal)
            || lower.Contains("403", StringComparison.Ordinal) || lower.Contains("forbidden", StringComparison.Ordinal))
        {
            return $"GitHub Packages {operation} failed for '{packageId}': GH_TOKEN/GITHUB_TOKEN is missing read access. Use a Classic PAT with read:packages; fine-grained PATs are unsupported by GitHub Packages NuGet. Raw error: {message}";
        }

        if (lower.Contains("404", StringComparison.Ordinal) || lower.Contains("not found", StringComparison.Ordinal))
        {
            return $"GitHub Packages {operation} failed for '{packageId}': package/feed was not found. The first publish wave may not have shipped yet, or the package id/feed URL is wrong. Raw error: {message}";
        }

        if (exceptions.Any(static ex => ex is HttpRequestException or IOException or TimeoutException))
        {
            return $"GitHub Packages {operation} failed for '{packageId}': network or feed availability problem. Retry, or check connectivity to {GitHubPackagesFeedUrl}. Raw error: {message}";
        }

        return $"GitHub Packages {operation} failed for '{packageId}'. Raw error: {message}";
    }

    // ── UI helpers ──

    public static void PrintSummaryTable(List<StepResult> results, TimeSpan totalDuration)
    {
        if (results.Count == 0) return;

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
    }

    private static string SanitizeForFile(string value)
    {
        var sanitized = new StringBuilder(value.Length);
        foreach (var ch in value)
            sanitized.Append(char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '-');
        return sanitized.ToString();
    }

    private static string QuoteArg(string argument)
        => argument.Contains(' ', StringComparison.Ordinal) ? $"\"{argument}\"" : argument;

    public static string BuildStepLogPath(string logDir, int stepNumber, string label)
        => Path.Combine(logDir, $"{stepNumber:00}-{SanitizeForFile(label)}.log");

    private static PipeTarget CreateLineLogPipe(string logPath, TextWriter? consoleWriter)
    {
        var sync = LineLogLocks.GetOrAdd(logPath, static _ => new System.Threading.Lock());
        return PipeTarget.ToDelegate(line =>
        {
            lock (sync)
            {
                File.AppendAllText(logPath, line + Environment.NewLine, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }

            consoleWriter?.WriteLine(line);
        });
    }

    private static IEnumerable<Exception> FlattenExceptions(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException!)
            yield return current;
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, System.Threading.Lock> LineLogLocks = new(StringComparer.OrdinalIgnoreCase);
}
