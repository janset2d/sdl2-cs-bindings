using System.Text.RegularExpressions;
using PublicApiGenerator;

namespace Janset.SDL2.ApiSnapshot;

public sealed partial class CoreApiSnapshotTests
{
    private static readonly string[] RawAbiMarkers =
    [
        "SDLNative",
        "LibraryImport",
        "DllImport",
        "UnmanagedCallConv",
        "NativeTypeName",
        "CLong",
        "CULong",
    ];

    [Test]
    public async Task SDL2_Core_public_api_is_unchanged()
    {
        var publicApi = GenerateCorePublicApi();

        await AssertPublicApiDoesNotLeakRawAbi(publicApi);
        await AssertDeferredEndianAliasesAreNotPublicConstants(publicApi);

        await Verify(publicApi);
    }

    private static string GenerateCorePublicApi()
    {
        return typeof(global::SDL2.SDL).Assembly.GeneratePublicApi();
    }

    private static async Task AssertPublicApiDoesNotLeakRawAbi(string publicApi)
    {
        foreach (var marker in RawAbiMarkers)
        {
            await Assert.That(publicApi).DoesNotContain(marker);
        }
    }

    private static async Task AssertDeferredEndianAliasesAreNotPublicConstants(string publicApi)
    {
        await Assert.That(PublicConstPattern("SDL_BYTEORDER").IsMatch(publicApi)).IsFalse();
        await Assert.That(PublicConstPattern("SDL_FLOATWORDORDER").IsMatch(publicApi)).IsFalse();
        await Assert.That(PublicAudioSysConstPattern().IsMatch(publicApi)).IsFalse();
        await Assert.That(PublicConstPattern("MIX_DEFAULT_FORMAT").IsMatch(publicApi)).IsFalse();
    }

    [GeneratedRegex(@"\bpublic\s+const\s+[^;=]+\s+AUDIO_[A-Z0-9]+SYS\s*=", RegexOptions.CultureInvariant)]
    private static partial Regex PublicAudioSysConstPattern();

    private static Regex PublicConstPattern(string name)
    {
        return new Regex(@"\bpublic\s+const\s+[^;=]+\s+" + Regex.Escape(name) + @"\s*=", RegexOptions.CultureInvariant);
    }
}
