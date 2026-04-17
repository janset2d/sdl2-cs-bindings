using SDL2;
using System.Runtime.InteropServices;

namespace PackageConsumer.Smoke;

/// <summary>
/// Shipping-graph smoke. Each test asserts one step of the Package Validation Mode
/// integration spine (see docs/research/execution-model-strategy-2026-04-13.md §7.2):
///
/// 1. Native assets land in the consumer's output directory.
/// 2. SDL_Init / SDL_Quit cycle succeeds (loader finds the native binary).
/// 3. Linked versions report expected upstream values.
///
/// Runs per TFM via TUnit + Microsoft Testing Platform. Invoke via Cake PostFlight
/// (per-TFM <c>dotnet test --filter Category=PackageSmoke -f &lt;tfm&gt;</c>), not directly.
/// </summary>
public sealed class PackageSmokeTests
{
    [Test]
    [Category("PackageSmoke")]
    public async Task Native_Core_Asset_Lands_In_Output()
    {
        var matches = EnumerateOutputFileNames();
        await Assert.That(matches.Any(IsCoreNativeAsset)).IsTrue();
    }

    [Test]
    [Category("PackageSmoke")]
    public async Task Native_Image_Asset_Lands_In_Output()
    {
        var matches = EnumerateOutputFileNames();
        await Assert.That(matches.Any(IsImageNativeAsset)).IsTrue();
    }

    [Test]
    [Category("PackageSmoke")]
    public async Task SDL_Init_Cycle_Succeeds()
    {
        var initResult = SDL.SDL_Init(0);
        try
        {
            await Assert.That(initResult).IsEqualTo(0);
        }
        finally
        {
            SDL.SDL_Quit();
        }
    }

    [Test]
    [Category("PackageSmoke")]
    public async Task Linked_Versions_Report_Expected_Majors()
    {
        // Initialize to force the native libraries to load before querying versions.
        SDL.SDL_Init(0);
        try
        {
            SDL.SDL_GetVersion(out var coreVersion);
            var imageVersion = SDL_image.IMG_Linked_Version();

            // SDL2 family always reports major=2. Specific minor/patch is tracked by
            // the upstream library version plane (manifest.json library_manifests[].vcpkg_version)
            // and validated by PreFlight G14/G15 — we just assert we linked against SDL2, not SDL3.
            await Assert.That(coreVersion.major).IsEqualTo((byte)2);
            await Assert.That(imageVersion.major).IsEqualTo((byte)2);
        }
        finally
        {
            SDL.SDL_Quit();
        }
    }

    private static string[] EnumerateOutputFileNames()
    {
        var baseDirectory = AppContext.BaseDirectory;
        return Directory
            .EnumerateFiles(baseDirectory, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetFileName(path) ?? string.Empty)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool IsCoreNativeAsset(string fileName)
    {
        if (IsWindowsPlatform())
        {
            return string.Equals(fileName, "SDL2.dll", StringComparison.OrdinalIgnoreCase);
        }

        // Unix: libSDL2-<version>.so.* or libSDL2-<version>.dylib.
        // Guard against the `libSDL2_image`/`libSDL2_mixer`/... prefix collision by
        // requiring the character after `libSDL2` to be `-` or `.`, not `_`.
        if (!HasPrefixFollowedBy(fileName, "libSDL2", '-', '.'))
        {
            return false;
        }

        return IsMacOsPlatform()
            ? fileName.EndsWith(".dylib", StringComparison.OrdinalIgnoreCase)
            : ContainsSharedObjectMarker(fileName);
    }

    private static bool IsImageNativeAsset(string fileName)
    {
        if (IsWindowsPlatform())
        {
            return string.Equals(fileName, "SDL2_image.dll", StringComparison.OrdinalIgnoreCase);
        }

        if (!fileName.StartsWith("libSDL2_image", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return IsMacOsPlatform()
            ? fileName.EndsWith(".dylib", StringComparison.OrdinalIgnoreCase)
            : ContainsSharedObjectMarker(fileName);
    }

    private static bool HasPrefixFollowedBy(string value, string prefix, params char[] allowedNextChars)
    {
        if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (value.Length == prefix.Length)
        {
            return false;
        }

        var nextChar = value[prefix.Length];
        foreach (var candidate in allowedNextChars)
        {
            if (nextChar == candidate)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsWindowsPlatform()
    {
#if NET5_0_OR_GREATER
    return OperatingSystem.IsWindows();
#else
    return RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
#endif
    }

    private static bool IsMacOsPlatform()
    {
#if NET5_0_OR_GREATER
    return OperatingSystem.IsMacOS();
#else
    return RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
#endif
    }

    private static bool ContainsSharedObjectMarker(string value)
    {
#if NET5_0_OR_GREATER
    return value.Contains(".so", StringComparison.OrdinalIgnoreCase);
#else
    return value.IndexOf(".so", StringComparison.OrdinalIgnoreCase) >= 0;
#endif
    }
}
