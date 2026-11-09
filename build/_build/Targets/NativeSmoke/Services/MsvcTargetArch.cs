namespace Build.Targets.NativeSmoke.Services;

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
