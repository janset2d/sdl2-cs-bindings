using System.Runtime.InteropServices;
using Cake.Core;

namespace Build.Host.Cake;

public static class CakePlatformExtensions
{
    /// <summary>
    /// Resolves the current host's .NET runtime identifier (<c>win-x64</c>, <c>linux-arm64</c>, <c>osx-x64</c>, …)
    /// from <see cref="ICakePlatform.Family"/> plus <see cref="RuntimeInformation.OSArchitecture"/>. This is the
    /// default value PathService consults when <c>--rid</c> is omitted, and the anchor for Harvest's per-RID
    /// deployment layout. Throws <see cref="PlatformNotSupportedException"/> on unrecognized OS family or CPU
    /// architecture so unsupported hosts fail loud rather than silently deriving a bogus RID string.
    /// </summary>
    public static string Rid(this ICakePlatform platform)
    {
        ArgumentNullException.ThrowIfNull(platform);

        var osPart = platform.Family switch
        {
            PlatformFamily.Windows => "win",
            PlatformFamily.Linux => "linux",
            PlatformFamily.OSX => "osx",
            _ => throw new PlatformNotSupportedException("Cannot determine OS platform for RID."),
        };
        var archPart = RuntimeInformation.OSArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            Architecture.X86 => "x86",
            Architecture.Arm => "arm",
            _ => throw new PlatformNotSupportedException($"Cannot determine OS architecture {RuntimeInformation.OSArchitecture} for RID."),
        };

        return $"{osPart}-{archPart}";
    }
}
