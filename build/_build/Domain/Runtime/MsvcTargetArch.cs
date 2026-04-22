using System.Runtime.InteropServices;

namespace Build.Domain.Runtime;

/// <summary>
/// Target architecture for an MSVC build, used to pick the correct
/// <c>vcvarsall.bat</c> argument. <c>vcvarsall.bat &lt;arg&gt;</c> combines the
/// host architecture (runner CPU) and the target architecture (what
/// <c>cl.exe</c> should emit):
/// <list type="bullet">
///   <item><description><c>x64</c>, <c>x86</c>, <c>arm64</c> — native (host == target)</description></item>
///   <item><description><c>x64_x86</c>, <c>x64_arm64</c>, <c>arm64_x64</c>, … — cross-compile (host != target)</description></item>
/// </list>
/// RID → target mapping is 1-to-1 with the three Windows RIDs this build
/// host supports: <c>win-x64</c> → <see cref="X64"/>, <c>win-x86</c> →
/// <see cref="X86"/>, <c>win-arm64</c> → <see cref="Arm64"/>.
/// </summary>
public enum MsvcTargetArch
{
    /// <summary>64-bit Intel/AMD target (<c>win-x64</c>).</summary>
    X64,

    /// <summary>32-bit Intel/AMD target (<c>win-x86</c>).</summary>
    X86,

    /// <summary>64-bit ARM target (<c>win-arm64</c>).</summary>
    Arm64,
}

/// <summary>
/// RID ↔ <see cref="MsvcTargetArch"/> conversions + <c>vcvarsall.bat</c> arg builder.
/// Kept as a domain-layer helper so <c>NativeSmokeTaskRunner</c> (Application) and
/// <c>MsvcDevEnvironment</c> (Infrastructure) can both consume it without pulling
/// shared arch logic into either layer.
/// </summary>
public static class MsvcTargetArchExtensions
{
    /// <summary>
    /// Maps a Windows RID to its MSVC target architecture. Fails fast on any
    /// RID outside the supported set — the build host intentionally does not
    /// fall back to a default, because a silent misdetection compiles the
    /// wrong binary and only surfaces at load time on an end-user machine.
    /// </summary>
    /// <exception cref="ArgumentException">When <paramref name="rid"/> is null or whitespace.</exception>
    /// <exception cref="PlatformNotSupportedException">
    /// When <paramref name="rid"/> is not <c>win-x64</c>, <c>win-x86</c>, or <c>win-arm64</c>.
    /// </exception>
    public static MsvcTargetArch FromRid(string rid)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rid);
        return rid switch
        {
            "win-x64" => MsvcTargetArch.X64,
            "win-x86" => MsvcTargetArch.X86,
            "win-arm64" => MsvcTargetArch.Arm64,
            _ => throw new PlatformNotSupportedException(
                $"MsvcTargetArch: no vcvarsall mapping for RID '{rid}'. " +
                "Supported Windows RIDs: win-x64, win-x86, win-arm64."),
        };
    }

    /// <summary>
    /// Builds the <c>vcvarsall.bat</c> argument for the current process host
    /// architecture (from <see cref="RuntimeInformation.OSArchitecture"/>) and
    /// the given <paramref name="target"/>. Returns just the target name when
    /// host == target (e.g. <c>x64</c>), or the <c>host_target</c> cross-compile
    /// form otherwise (e.g. <c>x64_arm64</c>).
    /// </summary>
    /// <exception cref="PlatformNotSupportedException">
    /// When the host architecture isn't one of x64 / x86 / arm64.
    /// </exception>
    public static string ToVcvarsArg(this MsvcTargetArch target)
    {
        return ToVcvarsArg(target, RuntimeInformation.OSArchitecture);
    }

    /// <summary>
    /// Overload taking an explicit host architecture. Exists for deterministic
    /// unit tests — production callers should use the parameter-less
    /// <see cref="ToVcvarsArg(MsvcTargetArch)"/> which reads the real host.
    /// </summary>
    internal static string ToVcvarsArg(this MsvcTargetArch target, Architecture hostArchitecture)
    {
        var hostArg = hostArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.X86 => "x86",
            Architecture.Arm64 => "arm64",
            var other => throw new PlatformNotSupportedException(
                $"MsvcTargetArch: unsupported host architecture '{other}' for vcvarsall.bat. " +
                "Supported host arches: x64, x86, arm64."),
        };

        var targetArg = target switch
        {
            MsvcTargetArch.X64 => "x64",
            MsvcTargetArch.X86 => "x86",
            MsvcTargetArch.Arm64 => "arm64",
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, "Unknown MsvcTargetArch value."),
        };

        return string.Equals(hostArg, targetArg, StringComparison.Ordinal)
            ? hostArg
            : $"{hostArg}_{targetArg}";
    }
}
