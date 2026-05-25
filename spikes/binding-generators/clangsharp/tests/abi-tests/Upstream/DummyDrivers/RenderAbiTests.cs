using Janset.SDL2.AbiTests.Infrastructure;
using Janset.SDL2.AbiTests.Infrastructure.Classification;
using Janset.SDL2.AbiTests.Infrastructure.Sdl;
using Janset.SDL2.AbiTests.Infrastructure.Sdl.Requirements;
using Janset.SDL2.AbiTests.Infrastructure.Sdl.Scopes;
using SDL2;
using static SDL2.SDLNative;

namespace Janset.SDL2.AbiTests.Upstream.DummyDrivers;

public sealed class RenderAbiTests
{
    [Test]
    [NotInParallel(AbiParallelKeys.Video)]
    [RequiresVideoDummyDriver]
    [Category(AbiCategories.UpstreamPort)]
    [Category(AbiCategories.DummyDriver)]
    [Category(AbiCategories.SdlRender)]
    [UpstreamSdlTest("test/testautomation_render.c", "render_testPrimitives")]
    public async Task SDLCreateRenderer_Should_Create_Software_Renderer_For_Dummy_Window()
    {
        RenderResult result = CreateAndClearSoftwareRenderer();

        await Assert.That(result.WindowCreated).IsTrue();
        await Assert.That(result.RendererCreated).IsTrue();
        await SdlAssert.Success(result.SetColorResult, "SDL_SetRenderDrawColor");
        await SdlAssert.Success(result.ClearResult, "SDL_RenderClear");
    }

    private static unsafe RenderResult CreateAndClearSoftwareRenderer()
    {
        using SdlEnvironmentScope videoDriver = new("SDL_VIDEODRIVER", "dummy");
        using SdlHintScope renderDriverHint = new("SDL_RENDER_DRIVER", "software");
        using SdlSubsystemScope video = new(SDL_INIT_VIDEO);
        using PinnedUtf8 title = SdlUtf8.Pin("Janset SDL2 ABI dummy renderer");

        SDL_Window window = SDL_CreateWindow(title.Pointer, 0, 0, 64, 64, (uint)SDL_WindowFlags.SDL_WINDOW_HIDDEN);
        SDL_Renderer renderer = SDL_Renderer.Null;
        int setColorResult = -1;
        int clearResult = -1;

        try
        {
            if (window.IsNotNull)
            {
                renderer = SDL_CreateRenderer(window, -1, (uint)SDL_RendererFlags.SDL_RENDERER_SOFTWARE);
                if (renderer.IsNotNull)
                {
                    setColorResult = SDL_SetRenderDrawColor(renderer, 1, 2, 3, 255);
                    clearResult = SDL_RenderClear(renderer);
                }
            }

            return new RenderResult(window.IsNotNull, renderer.IsNotNull, setColorResult, clearResult);
        }
        finally
        {
            if (renderer.IsNotNull)
            {
                SDL_DestroyRenderer(renderer);
            }

            if (window.IsNotNull)
            {
                SDL_DestroyWindow(window);
            }
        }
    }

    private sealed record RenderResult(bool WindowCreated, bool RendererCreated, int SetColorResult, int ClearResult);
}
