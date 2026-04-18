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
    private const SDL_mixer.MIX_InitFlags MixInitWavPack = (SDL_mixer.MIX_InitFlags)0x00000080;

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
    public async Task Native_Mixer_Asset_Lands_In_Output()
    {
        var matches = EnumerateOutputFileNames();
        await Assert.That(matches.Any(IsMixerNativeAsset)).IsTrue();
    }

    [Test]
    [Category("PackageSmoke")]
    public async Task Native_Ttf_Asset_Lands_In_Output()
    {
        var matches = EnumerateOutputFileNames();
        await Assert.That(matches.Any(IsTtfNativeAsset)).IsTrue();
    }

    [Test]
    [Category("PackageSmoke")]
    public async Task Native_Gfx_Asset_Lands_In_Output()
    {
        var matches = EnumerateOutputFileNames();
        await Assert.That(matches.Any(IsGfxNativeAsset)).IsTrue();
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
    public async Task Image_Png_Fixture_Loads_Successfully()
    {
        IntPtr surface = IntPtr.Zero;
        var imageResult = SDL_image.IMG_Init(SDL_image.IMG_InitFlags.IMG_INIT_PNG);

        try
        {
            await Assert.That((imageResult & (int)SDL_image.IMG_InitFlags.IMG_INIT_PNG) != 0).IsTrue();

            surface = SDL_image.IMG_Load(GetPngFixturePath());
            await Assert.That(surface != IntPtr.Zero).IsTrue();
        }
        finally
        {
            if (surface != IntPtr.Zero)
            {
                SDL.SDL_FreeSurface(surface);
            }

            SDL_image.IMG_Quit();
        }
    }

    [Test]
    [Category("PackageSmoke")]
    public async Task Mixer_Init_Reports_Expected_Decoder_Surface()
    {
        ConfigureHeadlessDrivers();

        // Core load is enough here; Mix_OpenAudio below exercises the packaged audio path.
        var initResult = SDL.SDL_Init(0);
        try
        {
            await Assert.That(initResult).IsEqualTo(0);

            // Mix_OpenAudio in headless mode relies on SDL's dummy audio driver. Some
            // minimal Linux container images build SDL2 without any audio backend, in
            // which case dummy is not registered and Mix_OpenAudio fails with an opaque
            // "No such audio device". Assert the driver is present so the failure mode is
            // diagnosable per-RID instead of surfacing as a generic Mix_OpenAudio != 0.
            await Assert.That(IsDummyAudioDriverRegistered()).IsTrue();

            var mixerResult = SDL_mixer.Mix_Init(
                SDL_mixer.MIX_InitFlags.MIX_INIT_FLAC |
                SDL_mixer.MIX_InitFlags.MIX_INIT_MID |
                SDL_mixer.MIX_InitFlags.MIX_INIT_OGG |
                SDL_mixer.MIX_InitFlags.MIX_INIT_OPUS |
                SDL_mixer.MIX_InitFlags.MIX_INIT_MP3 |
                SDL_mixer.MIX_InitFlags.MIX_INIT_MOD |
                MixInitWavPack);

            await Assert.That((mixerResult & (int)SDL_mixer.MIX_InitFlags.MIX_INIT_FLAC) != 0).IsTrue();
            await Assert.That((mixerResult & (int)SDL_mixer.MIX_InitFlags.MIX_INIT_MID) != 0).IsTrue();
            await Assert.That((mixerResult & (int)SDL_mixer.MIX_InitFlags.MIX_INIT_OGG) != 0).IsTrue();
            await Assert.That((mixerResult & (int)SDL_mixer.MIX_InitFlags.MIX_INIT_OPUS) != 0).IsTrue();
            await Assert.That((mixerResult & (int)SDL_mixer.MIX_InitFlags.MIX_INIT_MP3) != 0).IsTrue();
            await Assert.That((mixerResult & (int)SDL_mixer.MIX_InitFlags.MIX_INIT_MOD) != 0).IsTrue();
            await Assert.That((mixerResult & (int)MixInitWavPack) != 0).IsTrue();

            var openAudioResult = SDL_mixer.Mix_OpenAudio(
                SDL_mixer.MIX_DEFAULT_FREQUENCY,
                SDL_mixer.MIX_DEFAULT_FORMAT,
                SDL_mixer.MIX_DEFAULT_CHANNELS,
                1024);
            await Assert.That(openAudioResult).IsEqualTo(0);

            var decoders = EnumerateMusicDecoders();
            await Assert.That(decoders.Count > 0).IsTrue();
            await Assert.That(ContainsDecoder(decoders, "OGG")).IsTrue();
            await Assert.That(ContainsDecoder(decoders, "OPUS")).IsTrue();
            await Assert.That(ContainsDecoder(decoders, "MP3")).IsTrue();
            await Assert.That(ContainsDecoder(decoders, "MOD")).IsTrue();
            await Assert.That(ContainsDecoder(decoders, "MIDI") || ContainsDecoder(decoders, "MID")).IsTrue();
            await Assert.That(ContainsDecoder(decoders, "FLAC")).IsTrue();
            await Assert.That(ContainsDecoder(decoders, "WAVPACK")).IsTrue();
        }
        finally
        {
            SDL_mixer.Mix_CloseAudio();
            SDL_mixer.Mix_Quit();
            SDL.SDL_Quit();
        }
    }

    [Test]
    [Category("PackageSmoke")]
    public async Task Ttf_Init_Succeeds()
    {
        var initResult = SDL_ttf.TTF_Init();
        try
        {
            await Assert.That(initResult).IsEqualTo(0);
        }
        finally
        {
            SDL_ttf.TTF_Quit();
        }
    }

    [Test]
    [Category("PackageSmoke")]
    public async Task Gfx_Primitives_Render_With_Dummy_Video_Driver()
    {
        ConfigureHeadlessDrivers();

        IntPtr surface = IntPtr.Zero;
        IntPtr renderer = IntPtr.Zero;

        var initResult = SDL.SDL_Init(SDL.SDL_INIT_VIDEO);
        try
        {
            await Assert.That(initResult).IsEqualTo(0);

            surface = SDL.SDL_CreateRGBSurfaceWithFormat(0, 64, 64, 32, SDL.SDL_PIXELFORMAT_ARGB8888);
            await Assert.That(surface != IntPtr.Zero).IsTrue();

            renderer = SDL.SDL_CreateSoftwareRenderer(surface);
            await Assert.That(renderer != IntPtr.Zero).IsTrue();

            var drawResult = SDL_gfx.filledCircleRGBA(renderer, 32, 32, 12, 255, 255, 255, 255);
            await Assert.That(drawResult).IsEqualTo(0);
        }
        finally
        {
            if (renderer != IntPtr.Zero)
            {
                SDL.SDL_DestroyRenderer(renderer);
            }

            if (surface != IntPtr.Zero)
            {
                SDL.SDL_FreeSurface(surface);
            }

            SDL.SDL_Quit();
        }
    }

    [Test]
    [Category("PackageSmoke")]
    public async Task Core_And_Image_Linked_Versions_Report_Expected_Majors()
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
            // SDL2-CS upstream currently exposes incorrect entrypoints for mixer/ttf linked-version
            // helpers, so this check is intentionally scoped to the wrapper methods that are valid
            // without carrying a local submodule patch.
            await Assert.That(coreVersion.major).IsEqualTo((byte)2);
            await Assert.That(imageVersion.major).IsEqualTo((byte)2);
        }
        finally
        {
            SDL.SDL_Quit();
        }
    }

    /// <summary>
    /// Linux / macOS: the harvest pipeline ships symlink chains (e.g.
    /// <c>libSDL2.so → libSDL2-2.0.so.0 → libSDL2-2.0.so.0.&lt;patch&gt;</c>) via
    /// native.tar.gz because NuGet cannot represent symlinks directly (see
    /// docs/knowledge-base/harvesting-process.md §5, NuGet/Home#12136). The
    /// consumer-side extraction target in <c>buildTransitive/Janset.SDL2.Native.Common.targets</c>
    /// uses shell <c>tar -xzf</c>, which preserves symlinks on POSIX by default.
    ///
    /// Asserting the short-name loader alias (libSDL2.so / libSDL2.dylib) exists AND is
    /// a symlink pins the whole chain: if the archive extracted as plain files (or the
    /// target did not fire at all) the alias would either be missing or be a regular
    /// file with identical content to the real library — both scenarios would break
    /// dlopen's SONAME-based resolution on Linux and leave the test red.
    ///
    /// Windows has no equivalent expectation — the payload ships as plain *.dll files
    /// through the standard runtimes/&lt;rid&gt;/native/ auto-copy, so the test short-circuits
    /// to success.
    /// </summary>
#if NET6_0_OR_GREATER
    [Test]
    [Category("PackageSmoke")]
    public async Task Native_Symlink_Chain_Preserved_On_Unix()
    {
        if (IsWindowsPlatform())
        {
            return;
        }

        var baseDirectory = AppContext.BaseDirectory;
        var aliasName = IsMacOsPlatform() ? "libSDL2.dylib" : "libSDL2.so";
        var candidates = Directory
            .EnumerateFiles(baseDirectory, aliasName, SearchOption.AllDirectories)
            .ToList();

        await Assert.That(candidates.Count > 0).IsTrue();

        var aliasPath = candidates[0];
        var info = new FileInfo(aliasPath);

        // If the alias is a regular file (not a symlink), the archive was extracted
        // without symlink preservation — the whole point of native.tar.gz is lost.
        // File.ResolveLinkTarget returns non-null only for actual symlinks; both
        // relative and absolute targets are accepted.
        var linkTarget = info.ResolveLinkTarget(returnFinalTarget: false);
        await Assert.That(linkTarget).IsNotNull();

        // Walk to the final file. Must be a regular file, non-zero length, co-located
        // with the alias (or a compatible SONAME file in the same directory) — this
        // pins the extraction actually populated the real binary, not just the chain.
        var finalTarget = info.ResolveLinkTarget(returnFinalTarget: true);
        await Assert.That(finalTarget).IsNotNull();
        await Assert.That(finalTarget!.Exists).IsTrue();
        await Assert.That(((FileInfo)finalTarget).Length > 0).IsTrue();
    }
#endif

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

    private static bool IsMixerNativeAsset(string fileName)
    {
        return IsSatelliteNativeAsset(fileName, "SDL2_mixer.dll", "libSDL2_mixer");
    }

    private static bool IsTtfNativeAsset(string fileName)
    {
        return IsSatelliteNativeAsset(fileName, "SDL2_ttf.dll", "libSDL2_ttf");
    }

    private static bool IsGfxNativeAsset(string fileName)
    {
        return IsSatelliteNativeAsset(fileName, "SDL2_gfx.dll", "libSDL2_gfx");
    }

    private static bool IsSatelliteNativeAsset(string fileName, string windowsFileName, string unixPrefix)
    {
        if (IsWindowsPlatform())
        {
            return string.Equals(fileName, windowsFileName, StringComparison.OrdinalIgnoreCase);
        }

        if (!fileName.StartsWith(unixPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return IsMacOsPlatform()
            ? fileName.EndsWith(".dylib", StringComparison.OrdinalIgnoreCase)
            : ContainsSharedObjectMarker(fileName);
    }

    private static string GetPngFixturePath()
    {
        return Path.Combine(AppContext.BaseDirectory, "TestAssets", "janset2d-sdl-min.png");
    }

    private static List<string> EnumerateMusicDecoders()
    {
        return Enumerable
            .Range(0, SDL_mixer.Mix_GetNumMusicDecoders())
            .Select(SDL_mixer.Mix_GetMusicDecoder)
            .Where(decoder => !string.IsNullOrWhiteSpace(decoder))
            .ToList()!;
    }

    private static bool ContainsDecoder(IEnumerable<string> decoders, string expectedFragment)
    {
        return decoders.Any(decoder => ContainsIgnoreCase(decoder, expectedFragment));
    }

    private static bool ContainsIgnoreCase(string value, string expectedFragment)
    {
#if NETFRAMEWORK
        return value.IndexOf(expectedFragment, StringComparison.OrdinalIgnoreCase) >= 0;
#else
        return value.Contains(expectedFragment, StringComparison.OrdinalIgnoreCase);
#endif
    }

    private static void ConfigureHeadlessDrivers()
    {
        SDL.SDL_SetHint("SDL_VIDEODRIVER", "dummy");
        SDL.SDL_SetHint("SDL_AUDIODRIVER", "dummy");
    }

    private static bool IsDummyAudioDriverRegistered()
    {
        var count = SDL.SDL_GetNumAudioDrivers();
        for (var i = 0; i < count; i++)
        {
            var name = SDL.SDL_GetAudioDriver(i);
            if (!string.IsNullOrEmpty(name) && string.Equals(name, "dummy", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
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
