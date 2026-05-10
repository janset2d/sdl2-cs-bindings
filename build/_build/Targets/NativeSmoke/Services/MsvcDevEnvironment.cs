using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using Cake.Common.Tools.VSWhere;
using Cake.Common.Tools.VSWhere.Latest;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;

namespace Build.Targets.NativeSmoke.Services;

/// <summary>
/// Resolves the MSVC developer environment (variables normally set by
/// <c>vcvarsall.bat</c> / <c>vcvars64.bat</c>) that CMake + Ninja + <c>cl.exe</c>
/// need on Windows.
/// </summary>
/// <remarks>
/// <para>
/// <b>Windows-only by contract.</b> Callers must gate on
/// <see cref="OperatingSystem.IsWindows"/> before invoking
/// <see cref="ResolveAsync"/>; non-Windows invocations surface a
/// <see cref="PlatformNotSupportedException"/> as a defence-in-depth assertion.
/// The platform-gate lives at the call site on purpose: the resolver concerns
/// itself only with MSVC-specific discovery (VSWhere + <c>vcvarsall.bat</c>
/// execution + env delta parsing), and the CMake/Ninja/MSBuild consumers
/// decide when that work is meaningful.
/// </para>
/// <para>
/// Two return shapes on Windows:
/// </para>
/// <list type="bullet">
///   <item><description><b>Empty dictionary</b> — the parent shell already has
///     MSVC sourced (detected via <c>VCToolsInstallDir</c>, which every
///     <c>vcvarsXYZ.bat</c> sets). The child process inherits the working
///     toolchain without injection.</description></item>
///   <item><description><b>Delta dictionary</b> — the set of env vars the
///     current process is missing vs. what <c>vcvarsall.bat</c> emits.
///     Callers merge these into <c>ToolSettings.EnvironmentVariables</c> on
///     the child process, so only the missing/changed entries are pushed
///     (PATH gets its MSVC prefixes, INCLUDE/LIB/LIBPATH get populated,
///     etc.).</description></item>
/// </list>
/// <para>
/// Mirrors the dumpbin-self-resolution pattern (see <c>DumpbinTool</c>): Cake
/// finds the MSVC installation via VSWhere and lifts the needed environment
/// without forcing contributors or CI runners to launch from a Developer
/// PowerShell. Removes the need for external GitHub Actions such as
/// <c>ilammy/msvc-dev-cmd</c>.
/// </para>
/// </remarks>
public interface IMsvcDevEnvironment
{
    /// <summary>
    /// Returns the environment-variable delta to merge into child processes that
    /// invoke MSVC-dependent tooling (CMake Ninja + <c>cl.exe</c>, <c>msbuild.exe</c>
    /// command-line, etc.) for the requested <paramref name="targetArch"/>. The
    /// resolver shells out to <c>vcvarsall.bat &lt;host_target&gt;</c>, so x64 and
    /// cross-compile x86 / arm64 each get their own env layer. Thread-safe;
    /// resolution runs at most once per (target, process-lifetime) pair and is
    /// cached thereafter (per-arch cache so a RID matrix doesn't conflate layers).
    /// </summary>
    /// <exception cref="PlatformNotSupportedException">Thrown when invoked on a
    /// non-Windows host. The caller must gate on
    /// <see cref="OperatingSystem.IsWindows"/>.</exception>
    Task<IReadOnlyDictionary<string, string>> ResolveAsync(MsvcTargetArch targetArch, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class MsvcDevEnvironment : IMsvcDevEnvironment
{
    private const string VsToolsRequirement = "Microsoft.VisualStudio.Component.VC.Tools.x86.x64";
    private const string VcvarsAllRelativePath = "VC/Auxiliary/Build/vcvarsall.bat";
    private const string VcToolsInstallDirEnvVar = "VCToolsInstallDir";

    private static readonly IReadOnlyDictionary<string, string> Empty =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private readonly ICakeContext _cakeContext;
    private readonly ICakeLog _log;
    private readonly ConcurrentDictionary<MsvcTargetArch, IReadOnlyDictionary<string, string>> _cache = new();

    public MsvcDevEnvironment(ICakeContext cakeContext, ICakeLog log)
    {
        _cakeContext = cakeContext ?? throw new ArgumentNullException(nameof(cakeContext));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, string>> ResolveAsync(
        MsvcTargetArch targetArch,
        CancellationToken ct = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "MsvcDevEnvironment.ResolveAsync is Windows-only. Callers must gate on OperatingSystem.IsWindows() " +
                "before invoking the resolver; non-Windows hosts rely on gcc / clang already present on PATH.");
        }

        if (_cache.TryGetValue(targetArch, out var cached))
        {
            return cached;
        }

        var result = await ResolveCoreAsync(targetArch, ct).ConfigureAwait(false);
        // ConcurrentDictionary handles the race; a pathological concurrent invocation would
        // at worst cause two resolver runs with the later write winning — same outcome.
        _cache[targetArch] = result;
        return result;
    }

    private async Task<IReadOnlyDictionary<string, string>> ResolveCoreAsync(MsvcTargetArch targetArch, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        // Fast path: the caller's shell (Developer PowerShell, CI workflow that already
        // sourced vcvars, etc.) has MSVC live. VCToolsInstallDir is the canonical marker.
        // NOTE: the parent shell was sourced for SOME target arch, which we can't
        // introspect. Contract: if you source the parent shell, source it for the arch
        // that matches the RID you intend to build. CI paths never source the parent
        // shell so this fast-path short-circuits only on operator workstations.
        var vcToolsInstallDir = Environment.GetEnvironmentVariable(VcToolsInstallDirEnvVar);
        if (!string.IsNullOrWhiteSpace(vcToolsInstallDir))
        {
            _log.Verbose(
                "MsvcDevEnvironment: parent shell already has MSVC sourced ({0}={1}); no override needed for target '{2}'.",
                VcToolsInstallDirEnvVar,
                vcToolsInstallDir,
                targetArch);
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

        var vcvarsArg = targetArch.ToVcvarsArg();
        _log.Information(
            "MsvcDevEnvironment: sourcing MSVC environment for target '{0}' via '{1}' {2}",
            targetArch,
            vcvarsBat.FullPath,
            vcvarsArg);

        var delta = await CaptureEnvironmentDeltaAsync(vcvarsBat, vcvarsArg, ct).ConfigureAwait(false);
        _log.Information(
            "MsvcDevEnvironment: captured {0} env var(s) to merge into MSVC-dependent child processes for target '{1}'.",
            delta.Count,
            targetArch);
        return delta;
    }

    private static async Task<Dictionary<string, string>> CaptureEnvironmentDeltaAsync(
        FilePath vcvarsBat,
        string vcvarsArg,
        CancellationToken ct)
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
        commandLine.Append("/s /c \"\"").Append(vcvarsBat.FullPath).Append("\" ").Append(vcvarsArg)
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
        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        await process.WaitForExitAsync(ct).ConfigureAwait(false);
        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            throw new CakeException(
                $"MsvcDevEnvironment: 'cmd /c vcvarsall.bat {vcvarsArg}' exited with code {process.ExitCode}. " +
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
