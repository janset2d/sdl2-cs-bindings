using System.Runtime.InteropServices;

namespace Build.Targets.NativeSmoke.Services;

/// <summary>
/// RID ↔ <see cref="MsvcTargetArch"/> conversions + <c>vcvarsall.bat</c> arg builder.
/// Kept target-local so <c>NativeSmokeTask</c> and <c>MsvcDevEnvironment</c> share
/// one Windows RID mapping without duplicating MSVC architecture rules.
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
            Architecture.Arm or Architecture.Wasm or Architecture.S390x or Architecture.LoongArch64
                or Architecture.Armv6 or Architecture.Ppc64le or Architecture.RiscV64 => throw UnsupportedHostArchitecture(hostArchitecture),
            var other => throw UnsupportedHostArchitecture(other),
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

    private static PlatformNotSupportedException UnsupportedHostArchitecture(Architecture architecture) => new(
        $"MsvcTargetArch: unsupported host architecture '{architecture}' for vcvarsall.bat. " +
        "Supported host arches: x64, x86, arm64.");
}
