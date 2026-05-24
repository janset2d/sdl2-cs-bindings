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
[NotInParallel]
public sealed class ThreadIdAbiTests
{
    [Test]
    [Category("AbiSmoke")]
    public async Task SDL_ThreadID_Returns_NonZero_On_Host_Platform()
    {
#if NET6_0_OR_GREATER
        ulong threadId = (ulong)SDLNative.SDL_ThreadID().Value;
#else
        ulong threadId = SDLNative.SDL_ThreadID();
#endif

        await Assert.That(threadId).IsNotEqualTo(0UL);
    }
}
