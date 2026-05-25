using System.Runtime.InteropServices;
using Janset.SDL2.AbiTests.Infrastructure.Classification;
using Janset.SDL2.AbiTests.Infrastructure.Sdl;
using SDL2;
using static SDL2.SDLNative;

namespace Janset.SDL2.AbiTests.Upstream.Pure;

public sealed class PlatformAbiTests
{
    [Test]
    [Category(AbiCategories.UpstreamPort)]
    [Category(AbiCategories.Pure)]
    [Category(AbiCategories.SdlPlatform)]
    [UpstreamSdlTest("test/testautomation_platform.c", "platform_testGetFunctions")]
    public async Task SDLGetPlatform_Should_Return_NonEmpty_String()
    {
        string platform = GetPlatform();

        await Assert.That(platform).IsNotEmpty();
    }

    [Test]
    [Category(AbiCategories.HeaderCoverage)]
    [Category(AbiCategories.Pure)]
    [Category(AbiCategories.SdlPlatform)]
    public async Task SDLGetPlatform_Should_Return_Current_Os_Platform_String()
    {
        string platform = GetPlatform();

        await Assert.That(platform).IsEqualTo(GetExpectedPlatform());
    }

    [Test]
    [Category(AbiCategories.UpstreamPort)]
    [Category(AbiCategories.Pure)]
    [Category(AbiCategories.SdlPlatform)]
    [UpstreamSdlTest("test/testautomation_platform.c", "platform_testGetVersion")]
    public async Task SDLGetVersion_Should_Report_Sdl2_Version()
    {
        SDL_version version = GetVersion();

        await Assert.That(version.major).IsEqualTo((byte)SDL_MAJOR_VERSION);
        await Assert.That(version.minor).IsGreaterThanOrEqualTo((byte)SDL_MINOR_VERSION);
    }

    [Test]
    [Category(AbiCategories.HeaderCoverage)]
    [Category(AbiCategories.Pure)]
    [Category(AbiCategories.SdlPlatform)]
    public async Task SDLGetVersion_Should_Match_Generated_Version_Constants()
    {
        SDL_version version = GetVersion();

        await Assert.That(version.major).IsEqualTo((byte)SDL_MAJOR_VERSION);
        await Assert.That(version.minor).IsEqualTo((byte)SDL_MINOR_VERSION);
        await Assert.That(version.patch).IsEqualTo((byte)SDL_PATCHLEVEL);
    }

    [Test]
    [Category(AbiCategories.HeaderCoverage)]
    [Category(AbiCategories.Pure)]
    [Category(AbiCategories.SdlPlatform)]
    public async Task SDLCompiledVersion_Should_Match_Generated_Version_Constants()
    {
        int expected = (SDL_MAJOR_VERSION * 1000) + (SDL_MINOR_VERSION * 100) + SDL_PATCHLEVEL;
        int actual = SDL_COMPILEDVERSION;

        await Assert.That(actual).IsEqualTo(expected);
    }

    [Test]
    [Category(AbiCategories.UpstreamPort)]
    [Category(AbiCategories.Pure)]
    [Category(AbiCategories.SdlPlatform)]
    [UpstreamSdlTest("test/testautomation_platform.c", "platform_testGetFunctions")]
    public async Task SDLGetRevision_Should_Return_NonNull_Pointer()
    {
        bool revisionIsNotNull = IsRevisionPointerNotNull();

        await Assert.That(revisionIsNotNull).IsTrue();
    }

    [Test]
    [NotInParallel(AbiParallelKeys.Error)]
    [Category(AbiCategories.UpstreamPort)]
    [Category(AbiCategories.GlobalState)]
    [Category(AbiCategories.SdlPlatform)]
    [Category(AbiCategories.SdlError)]
    [UpstreamSdlTest("test/testautomation_platform.c", "platform_testGetSetClearError")]
    public async Task SDLGetSetClearError_Should_RoundTrip_Error_String()
    {
        SdlError.Clear();
        await Assert.That(SdlError.Current).IsEmpty();

        using PinnedUtf8 error = SdlUtf8.Pin("Testing");
        int result = SetError(error);

        await Assert.That(result).IsEqualTo(-1);
        await Assert.That(SdlError.Current).IsEqualTo("Testing");

        SdlError.Clear();
        await Assert.That(SdlError.Current).IsEmpty();
    }

    [Test]
    [NotInParallel(AbiParallelKeys.Error)]
    [Category(AbiCategories.UpstreamPort)]
    [Category(AbiCategories.GlobalState)]
    [Category(AbiCategories.SdlPlatform)]
    [Category(AbiCategories.SdlError)]
    [UpstreamSdlTest("test/testautomation_platform.c", "platform_testSetErrorEmptyInput")]
    public async Task SDLSetError_Should_RoundTrip_Empty_Error_String()
    {
        SdlError.Clear();

        using PinnedUtf8 error = SdlUtf8.Pin(string.Empty);
        int result = SetError(error);

        await Assert.That(result).IsEqualTo(-1);
        await Assert.That(SdlError.Current).IsEmpty();

        SdlError.Clear();
        await Assert.That(SdlError.Current).IsEmpty();
    }

    private static string GetExpectedPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return "Windows";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return "Linux";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return "Mac OS X";
        }

        throw new PlatformNotSupportedException($"No deterministic SDL_GetPlatform expectation for {RuntimeInformation.OSDescription}.");
    }

    private static unsafe string GetPlatform()
    {
        return SdlUtf8.FromNullTerminated(SDL_GetPlatform());
    }

    private static unsafe SDL_version GetVersion()
    {
        SDL_version version = default;
        SDL_GetVersion(&version);

        return version;
    }

    private static unsafe bool IsRevisionPointerNotNull()
    {
        return SDL_GetRevision() is not null;
    }

    private static unsafe int SetError(PinnedUtf8 error)
    {
        return SDL_SetError(error.Pointer);
    }
}
