using Janset.SDL2.AbiTests.Infrastructure.Classification;
using SDL2;

namespace Janset.SDL2.AbiTests;

/// <summary>
/// Layer 1 raw ABI smoke for the SDL_ThreadID family. Exercises the
/// ThreadIdDualDispatchRewriter's per-mode output:
///   - Modern TFMs (net8+): CULong return via [LibraryImport].
///   - Compat TFM (net462): managed ulong wrapper + RuntimeInformation
///     dispatch + dual private [DllImport] (Win32 uint / Unix64 nint).
///
/// On Windows host, both modes dispatch through the 32-bit Win32 branch.
/// The Unix64 nint path is exercised by the CI per-RID matrix. The build
/// itself (per-TFM compile of this project against Janset.SDL2.Core) is
/// also evidence — if the rewriter's output were invalid on net462 where
/// CULong does not exist, the build would fail.
/// </summary>
public sealed class ThreadIdAbiTests
{
    [Test]
    [Category(AbiCategories.Smoke)]
    [Category(AbiCategories.SdlThread)]
    public async Task SDL_ThreadID_Returns_NonZero_On_Host_Platform()
    {
        ulong threadId = GetCurrentThreadId();

        await Assert.That(threadId).IsNotEqualTo(0UL);
    }

    [Test]
    [Category(AbiCategories.Smoke)]
    [Category(AbiCategories.SdlThread)]
    public async Task SDLGetThreadID_Should_Return_Current_Thread_Id_When_Thread_Is_Null()
    {
        ulong currentThreadId = GetCurrentThreadId();
        ulong queriedThreadId = GetThreadId(SDL_Thread.Null);

        await Assert.That(queriedThreadId).IsEqualTo(currentThreadId);
    }

    private static ulong GetCurrentThreadId()
    {
#if NET6_0_OR_GREATER
        return (ulong)SDLNative.SDL_ThreadID().Value;
#else
        return SDLNative.SDL_ThreadID();
#endif
    }

    private static ulong GetThreadId(SDL_Thread thread)
    {
#if NET6_0_OR_GREATER
        return (ulong)SDLNative.SDL_GetThreadID(thread).Value;
#else
        return SDLNative.SDL_GetThreadID(thread);
#endif
    }
}
