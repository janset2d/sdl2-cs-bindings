using Janset.SDL2.AbiTests.Infrastructure.Classification;
using Janset.SDL2.AbiTests.Infrastructure.Sdl;
using Janset.SDL2.AbiTests.Infrastructure.Sdl.Requirements;
using Janset.SDL2.AbiTests.Infrastructure.Sdl.Scopes;
using SDL2;
using static SDL2.SDLNative;

namespace Janset.SDL2.AbiTests.Upstream.DummyDrivers;

public sealed class VideoAbiTests
{
    [Test]
    [NotInParallel(AbiParallelKeys.Video)]
    [RequiresVideoDummyDriver]
    [Category(AbiCategories.UpstreamPort)]
    [Category(AbiCategories.DummyDriver)]
    [Category(AbiCategories.SdlVideo)]
    [UpstreamSdlTest("test/testautomation_video.c", "video_createWindowVariousFlags")]
    public async Task SDLCreateWindow_Should_Create_And_Destroy_Dummy_Window()
    {
        WindowResult result = CreateAndDestroyDummyWindow();

        await Assert.That(result.Created).IsTrue();
    }

    private static unsafe WindowResult CreateAndDestroyDummyWindow()
    {
        using SdlEnvironmentScope videoDriver = new("SDL_VIDEODRIVER", "dummy");
        using SdlSubsystemScope video = new(SDL_INIT_VIDEO);
        using PinnedUtf8 title = SdlUtf8.Pin("Janset SDL2 ABI dummy window");

        SDL_Window window = SDL_CreateWindow(title.Pointer, 0, 0, 64, 64, (uint)SDL_WindowFlags.SDL_WINDOW_HIDDEN);
        if (window.IsNotNull)
        {
            SDL_DestroyWindow(window);
        }

        return new WindowResult(window.IsNotNull);
    }

    private sealed record WindowResult(bool Created);
}
