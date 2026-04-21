namespace Build.Domain.Runtime;

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
    /// command-line, etc.). Thread-safe; resolution runs at most once per process
    /// lifetime and is cached thereafter.
    /// </summary>
    /// <exception cref="PlatformNotSupportedException">Thrown when invoked on a
    /// non-Windows host. The caller must gate on
    /// <see cref="OperatingSystem.IsWindows"/>.</exception>
    Task<IReadOnlyDictionary<string, string>> ResolveAsync(CancellationToken cancellationToken = default);
}
