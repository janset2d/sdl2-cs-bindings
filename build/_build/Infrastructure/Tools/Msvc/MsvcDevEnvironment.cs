using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Build.Domain.Runtime;
using Cake.Common.Tools.VSWhere;
using Cake.Common.Tools.VSWhere.Latest;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;

namespace Build.Infrastructure.Tools.Msvc;

/// <summary>
/// Default <see cref="IMsvcDevEnvironment"/> implementation. Mirrors the self-resolution
/// pattern that <c>DumpbinTool</c> uses for the <c>dumpbin.exe</c> path, extended to
/// cover the full MSVC toolchain environment that <c>cl.exe</c> + Ninja need. Resolves
/// at most once per session (cached <see cref="Task{TResult}"/> behind a <see cref="Lazy{T}"/>).
/// </summary>
public sealed class MsvcDevEnvironment : IMsvcDevEnvironment
{
    private const string VsToolsRequirement = "Microsoft.VisualStudio.Component.VC.Tools.x86.x64";
    private const string VcvarsAllRelativePath = "VC/Auxiliary/Build/vcvarsall.bat";
    private const string VcToolsInstallDirEnvVar = "VCToolsInstallDir";

    private static readonly IReadOnlyDictionary<string, string> Empty =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private readonly ICakeContext _cakeContext;
    private readonly ICakeLog _log;
    private IReadOnlyDictionary<string, string>? _cached;

    public MsvcDevEnvironment(ICakeContext cakeContext, ICakeLog log)
    {
        _cakeContext = cakeContext ?? throw new ArgumentNullException(nameof(cakeContext));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public async Task<IReadOnlyDictionary<string, string>> ResolveAsync(CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "MsvcDevEnvironment.ResolveAsync is Windows-only. Callers must gate on OperatingSystem.IsWindows() " +
                "before invoking the resolver; non-Windows hosts rely on gcc / clang already present on PATH.");
        }

        // Cache is set once per session. Reference-type writes are atomic in .NET, and the
        // Cake host is single-threaded in practice — a pathological concurrent invocation
        // would at worst cause two resolver runs, the later write winning. No lock needed;
        // the no-lock shape also avoids the VSTHRD011 deadlock risk around `Lazy<Task<T>>`.
        var snapshot = _cached;
        if (snapshot is not null)
        {
            return snapshot;
        }

        snapshot = await ResolveCoreAsync(cancellationToken);
        _cached = snapshot;
        return snapshot;
    }

    private async Task<IReadOnlyDictionary<string, string>> ResolveCoreAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Fast path: the caller's shell (Developer PowerShell, CI workflow that already
        // sourced vcvars, etc.) has MSVC live. VCToolsInstallDir is the canonical marker.
        var vcToolsInstallDir = Environment.GetEnvironmentVariable(VcToolsInstallDirEnvVar);
        if (!string.IsNullOrWhiteSpace(vcToolsInstallDir))
        {
            _log.Verbose(
                "MsvcDevEnvironment: parent shell already has MSVC sourced ({0}={1}); no override needed.",
                VcToolsInstallDirEnvVar,
                vcToolsInstallDir);
            return Empty;
        }

        var vsInstallation = _cakeContext.VSWhereLatest(new VSWhereLatestSettings
        {
            Requires = VsToolsRequirement,
            ReturnProperty = "installationPath",
        });

        if (vsInstallation is null || string.IsNullOrWhiteSpace(vsInstallation.FullPath))
        {
            throw new CakeException(
                "MsvcDevEnvironment could not locate a Visual Studio installation with the 'Desktop development with C++' (VC Tools) workload via VSWhere. " +
                "Install VS Build Tools 2022+ with that workload, or invoke Cake from a Developer PowerShell for VS 2022 so VCToolsInstallDir is already set.");
        }

        var vcvarsBat = vsInstallation.CombineWithFilePath(new FilePath(VcvarsAllRelativePath));
        if (!File.Exists(vcvarsBat.FullPath))
        {
            throw new CakeException(
                $"MsvcDevEnvironment: vcvarsall.bat not found at '{vcvarsBat.FullPath}' under Visual Studio installation '{vsInstallation.FullPath}'. " +
                "Reinstall the VC Tools workload or run Cake from a Developer PowerShell.");
        }

        var arch = ResolveHostArchitectureArg();
        _log.Information("MsvcDevEnvironment: sourcing MSVC environment via '{0}' {1}", vcvarsBat.FullPath, arch);

        var delta = await CaptureEnvironmentDeltaAsync(vcvarsBat, arch, cancellationToken);
        _log.Information("MsvcDevEnvironment: captured {0} env var(s) to merge into MSVC-dependent child processes.", delta.Count);
        return delta;
    }

    private static string ResolveHostArchitectureArg()
    {
        return RuntimeInformation.OSArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.X86 => "x86",
            Architecture.Arm64 => "arm64",
            var other => throw new PlatformNotSupportedException(
                $"MsvcDevEnvironment: unsupported host architecture '{other}' for vcvarsall.bat. Supported host arches: x64, x86, arm64."),
        };
    }

    private static async Task<Dictionary<string, string>> CaptureEnvironmentDeltaAsync(
        FilePath vcvarsBat,
        string arch,
        CancellationToken cancellationToken)
    {
        // Shell out to cmd.exe so vcvarsall.bat can set env vars in its own shell scope,
        // then dump them via `set`. We capture stdout, parse KEY=VALUE, and diff against
        // the current process env to emit only the delta the caller needs to merge.
        //
        // Quoting: outer pair around the full /c argument preserves the inner quoted
        // vcvarsall.bat path across cmd.exe's argument lexer (classic nested-quoting
        // trick — see the cmd.exe /s flag docs). `/s` tells cmd to preserve the outer
        // quotes rather than strip them.
        var commandLine = new StringBuilder();
        commandLine.Append("/s /c \"\"").Append(vcvarsBat.FullPath).Append("\" ").Append(arch)
            .Append(" >nul && set\"");

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = commandLine.ToString(),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            },
        };

        process.Start();
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);
        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            throw new CakeException(
                $"MsvcDevEnvironment: 'cmd /c vcvarsall.bat {arch}' exited with code {process.ExitCode}. " +
                $"stderr: {stderr.Trim()}");
        }

        var delta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawLine in stdout.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (line.Length == 0)
            {
                continue;
            }

            var equalIndex = line.IndexOf('=', StringComparison.Ordinal);
            if (equalIndex <= 0)
            {
                continue;
            }

            var key = line[..equalIndex];
            var value = line[(equalIndex + 1)..];

            // Skip pseudo-variables cmd emits (e.g., "=::", "=C:") and keys containing
            // whitespace which cannot be real env var names.
            if (key.StartsWith('=') || key.Contains(' ', StringComparison.Ordinal))
            {
                continue;
            }

            var current = Environment.GetEnvironmentVariable(key);
            if (current is null || !string.Equals(current, value, StringComparison.Ordinal))
            {
                delta[key] = value;
            }
        }

        return delta;
    }
}
