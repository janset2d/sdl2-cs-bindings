using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;
using Build.Domain.Runtime;
using Cake.Core.Diagnostics;

namespace Build.Infrastructure.DotNet;

/// <summary>
/// Resolves RID-specific child-process runtime overrides for <c>dotnet</c>-hosted smoke
/// executions. Today this means bootstrapping x86 .NET runtimes on Windows for
/// <c>PackageConsumerSmoke --rid win-x86</c> while leaving the parent Cake host on x64.
/// </summary>
[SuppressMessage(
    "Minor Code Smell",
    "S1075:URIs should not be hardcoded",
    Justification = "Official Microsoft dotnet-install endpoint used deliberately for CI/runtime bootstrap.")]
public sealed partial class DotNetRuntimeEnvironment(ICakeLog log) : IDotNetRuntimeEnvironment
{
    private const string WinX86Rid = "win-x86";
    private const string DotNetRootX86 = "DOTNET_ROOT_X86";
    private const string DotNetRootX86Legacy = "DOTNET_ROOT(x86)";
    private static readonly Uri InstallScriptUri = new("https://dot.net/v1/dotnet-install.ps1", UriKind.Absolute);

    private static readonly IReadOnlyDictionary<string, string> Empty =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private readonly ICakeLog _log = log ?? throw new ArgumentNullException(nameof(log));
    private readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, string>> _cache = new(StringComparer.OrdinalIgnoreCase);

    public async Task<IReadOnlyDictionary<string, string>> ResolveAsync(
        string rid,
        IReadOnlyList<string> targetFrameworks,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rid);
        ArgumentNullException.ThrowIfNull(targetFrameworks);

        if (!string.Equals(rid, WinX86Rid, StringComparison.OrdinalIgnoreCase))
        {
            return Empty;
        }

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "DotNetRuntimeEnvironment.ResolveAsync only supports win-x86 smoke on a Windows host. " +
                "The caller requested RID 'win-x86' on a non-Windows machine.");
        }

        var runtimeChannels = ResolveRuntimeChannels(targetFrameworks);
        if (runtimeChannels.Count == 0)
        {
            _log.Verbose(
                "DotNetRuntimeEnvironment: RID '{0}' requested x86 runtime bootstrap, but TFM set [{1}] contains no modern .NET runtime targets.",
                rid,
                string.Join(", ", targetFrameworks));
            return Empty;
        }

        var cacheKey = string.Join('+', runtimeChannels);
        if (_cache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var resolved = await ResolveCoreAsync(runtimeChannels, cancellationToken);
        _cache[cacheKey] = resolved;
        return resolved;
    }

    internal static IReadOnlyList<string> ResolveRuntimeChannels(IReadOnlyList<string> targetFrameworks)
    {
        ArgumentNullException.ThrowIfNull(targetFrameworks);

        var channels = new SortedSet<int>();
        foreach (var tfm in targetFrameworks)
        {
            if (string.IsNullOrWhiteSpace(tfm))
            {
                continue;
            }

            if (tfm.StartsWith("netstandard", StringComparison.OrdinalIgnoreCase) ||
                tfm.StartsWith("net4", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var match = ModernNetTfmRegex().Match(tfm);
            if (!match.Success)
            {
                continue;
            }

            if (!int.TryParse(match.Groups["major"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var major) || major < 5)
            {
                continue;
            }

            channels.Add(major);
        }

        return channels.Select(major => $"{major}.0").ToArray();
    }

    private async Task<IReadOnlyDictionary<string, string>> ResolveCoreAsync(
        IReadOnlyList<string> runtimeChannels,
        CancellationToken cancellationToken)
    {
        var cacheRoot = Path.Combine(Path.GetTempPath(), "janset-sdl2", "dotnet-runtime-env");
        var x86Root = Path.Combine(cacheRoot, "x86");
        var installScriptPath = Path.Combine(cacheRoot, "dotnet-install.ps1");

        Directory.CreateDirectory(cacheRoot);
        Directory.CreateDirectory(x86Root);

        await EnsureInstallScriptAsync(installScriptPath, cancellationToken);

        foreach (var channel in runtimeChannels)
        {
            await InstallRuntimeAsync(installScriptPath, x86Root, channel, cancellationToken);
        }

        _log.Information(
            "DotNetRuntimeEnvironment: prepared x86 runtimes [{0}] under '{1}'. Child smoke apphosts will receive {2} + {3}.",
            string.Join(", ", runtimeChannels),
            x86Root,
            DotNetRootX86,
            DotNetRootX86Legacy);

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [DotNetRootX86] = x86Root,
            [DotNetRootX86Legacy] = x86Root,
        };
    }

    private static async Task EnsureInstallScriptAsync(string installScriptPath, CancellationToken cancellationToken)
    {
        if (File.Exists(installScriptPath))
        {
            return;
        }

        using var client = new HttpClient();
        await using var input = await client.GetStreamAsync(InstallScriptUri, cancellationToken);
        await using var output = File.Create(installScriptPath);
        await input.CopyToAsync(output, cancellationToken);
    }

    private async Task InstallRuntimeAsync(
        string installScriptPath,
        string installRoot,
        string channel,
        CancellationToken cancellationToken)
    {
        var powershellExecutable = ResolvePowerShellExecutable();

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = powershellExecutable,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };

        process.StartInfo.ArgumentList.Add("-NoLogo");
        process.StartInfo.ArgumentList.Add("-NoProfile");
        process.StartInfo.ArgumentList.Add("-NonInteractive");
        process.StartInfo.ArgumentList.Add("-ExecutionPolicy");
        process.StartInfo.ArgumentList.Add("Bypass");
        process.StartInfo.ArgumentList.Add("-File");
        process.StartInfo.ArgumentList.Add(installScriptPath);
        process.StartInfo.ArgumentList.Add("-Architecture");
        process.StartInfo.ArgumentList.Add("x86");
        process.StartInfo.ArgumentList.Add("-Channel");
        process.StartInfo.ArgumentList.Add(channel);
        process.StartInfo.ArgumentList.Add("-Runtime");
        process.StartInfo.ArgumentList.Add("dotnet");
        process.StartInfo.ArgumentList.Add("-InstallDir");
        process.StartInfo.ArgumentList.Add(installRoot);

        process.Start();
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);
        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"DotNetRuntimeEnvironment: x86 runtime install for channel '{channel}' failed with exit code {process.ExitCode}.{Environment.NewLine}" +
                stdout + Environment.NewLine + stderr);
        }

        _log.Verbose(
            "DotNetRuntimeEnvironment: installed/refreshed x86 dotnet runtime channel '{0}' into '{1}'.",
            channel,
            installRoot);
    }

    private static string ResolvePowerShellExecutable()
    {
        var pwshPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "PowerShell",
            "7",
            "pwsh.exe");

        return File.Exists(pwshPath) ? pwshPath : "powershell.exe";
    }

    [GeneratedRegex(
        @"^net(?<major>\d+)\.\d+(?:[-].+)?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking,
        matchTimeoutMilliseconds: 1000)]
    private static partial Regex ModernNetTfmRegex();
}
