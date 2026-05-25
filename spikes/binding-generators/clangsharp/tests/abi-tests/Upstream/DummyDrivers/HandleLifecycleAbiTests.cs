using Janset.SDL2.AbiTests.Infrastructure;
using Janset.SDL2.AbiTests.Infrastructure.Classification;
using Janset.SDL2.AbiTests.Infrastructure.Sdl.Requirements;
using Janset.SDL2.AbiTests.Infrastructure.Sdl.Scopes;
using SDL2;
using static SDL2.SDLNative;

namespace Janset.SDL2.AbiTests.Upstream.DummyDrivers;

public sealed class HandleLifecycleAbiTests
{
    [Test]
    [NotInParallel(AbiParallelKeys.Video)]
    [RequiresVideoDummyDriver]
    [Category(AbiCategories.Smoke)]
    [Category(AbiCategories.Mechanical)]
    [Category(AbiCategories.DummyDriver)]
    [Category(AbiCategories.SdlVideo)]
    [Category(AbiCategories.SdlRender)]
    public async Task SDLCreateWindowAndRenderer_Should_Create_And_RoundTrip_Opaque_Handles()
    {
        HandleLifecycleResult result = CreateRoundTripAndDestroyWindowRendererHandles();

        await SdlAssert.Success(result.CreateResult, "SDL_CreateWindowAndRenderer");
        await Assert.That(result.WindowCreated).IsTrue();
        await Assert.That(result.RendererCreated).IsTrue();
        await Assert.That(result.WindowRendererMatches).IsTrue();
        await Assert.That(result.RendererWindowMatches).IsTrue();
    }

    private static unsafe HandleLifecycleResult CreateRoundTripAndDestroyWindowRendererHandles()
    {
        using SdlEnvironmentScope videoDriver = new("SDL_VIDEODRIVER", "dummy");
        using SdlHintScope renderDriverHint = new("SDL_RENDER_DRIVER", "software");
        using SdlSubsystemScope video = new(SDL_INIT_VIDEO);

        SDL_Window* windowPointer = null;
        SDL_Renderer* rendererPointer = null;
        SDL_Window window = SDL_Window.Null;
        SDL_Renderer renderer = SDL_Renderer.Null;

        int createResult = SDL_CreateWindowAndRenderer(
            64,
            64,
            (uint)SDL_WindowFlags.SDL_WINDOW_HIDDEN,
            &windowPointer,
            &rendererPointer);

        try
        {
            window = (SDL_Window)(nint)windowPointer;
            renderer = (SDL_Renderer)(nint)rendererPointer;

            SDL_Renderer roundTripRenderer = window.IsNotNull ? SDL_GetRenderer(window) : SDL_Renderer.Null;
            SDL_Window roundTripWindow = renderer.IsNotNull ? SDL_RenderGetWindow(renderer) : SDL_Window.Null;

            return new HandleLifecycleResult(
                createResult,
                window.IsNotNull,
                renderer.IsNotNull,
                roundTripRenderer.Equals(renderer),
                roundTripWindow.Equals(window));
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

    private sealed record HandleLifecycleResult(
        int CreateResult,
        bool WindowCreated,
        bool RendererCreated,
        bool WindowRendererMatches,
        bool RendererWindowMatches);
}
