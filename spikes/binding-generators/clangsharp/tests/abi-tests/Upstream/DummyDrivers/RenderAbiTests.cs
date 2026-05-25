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
    [Category(AbiCategories.BehaviorCoverage)]
    [Category(AbiCategories.DummyDriver)]
    [Category(AbiCategories.SdlRender)]
    [UpstreamSdlTest("test/testautomation_render.c", "render_testGetNumRenderDrivers")]
    public async Task SDLGetNumRenderDrivers_Should_Return_At_Least_One_Render_Driver()
    {
        int driverCount = GetDummyRenderDriverCount();

        await Assert.That(driverCount).IsGreaterThanOrEqualTo(1);
    }

    [Test]
    [NotInParallel(AbiParallelKeys.Video)]
    [RequiresVideoDummyDriver]
    [Category(AbiCategories.Smoke)]
    [Category(AbiCategories.Mechanical)]
    [Category(AbiCategories.DummyDriver)]
    [Category(AbiCategories.SdlRender)]
    public async Task SDLCreateRenderer_Should_Create_Software_Renderer_For_Dummy_Window()
    {
        RenderResult result = CreateAndClearSoftwareRenderer();

        await Assert.That(result.WindowCreated).IsTrue();
        await Assert.That(result.RendererCreated).IsTrue();
        await SdlAssert.Success(result.SetColorResult, "SDL_SetRenderDrawColor");
        await SdlAssert.Success(result.ClearResult, "SDL_RenderClear");
    }

    [Test]
    [NotInParallel(AbiParallelKeys.Video)]
    [RequiresVideoDummyDriver]
    [Category(AbiCategories.Smoke)]
    [Category(AbiCategories.BehaviorCoverage)]
    [Category(AbiCategories.DummyDriver)]
    [Category(AbiCategories.SdlRender)]
    public async Task SDLRenderPrimitives_Should_Draw_With_Software_Renderer_For_Dummy_Window()
    {
        RenderPrimitivesResult result = DrawSoftwareRendererPrimitives();

        await Assert.That(result.WindowCreated).IsTrue();
        await Assert.That(result.RendererCreated).IsTrue();
        await SdlAssert.Success(result.ClearColorResult, "SDL_SetRenderDrawColor(clear)");
        await SdlAssert.Success(result.ClearResult, "SDL_RenderClear");
        await SdlAssert.Success(result.FillColorResult, "SDL_SetRenderDrawColor(fill)");
        await SdlAssert.Success(result.FillRectResult, "SDL_RenderFillRect");
        await SdlAssert.Success(result.PointColorResult, "SDL_SetRenderDrawColor(point)");
        await SdlAssert.Success(result.DrawPointResult, "SDL_RenderDrawPoint");
        await SdlAssert.Success(result.LineColorResult, "SDL_SetRenderDrawColor(line)");
        await SdlAssert.Success(result.DrawLineResult, "SDL_RenderDrawLine");
    }

    [Test]
    [NotInParallel(AbiParallelKeys.Video)]
    [RequiresVideoDummyDriver]
    [Category(AbiCategories.Smoke)]
    [Category(AbiCategories.BehaviorCoverage)]
    [Category(AbiCategories.DummyDriver)]
    [Category(AbiCategories.SdlRender)]
    public async Task SDLCreateTextureFromSurface_Should_Query_And_Copy_Texture_With_Software_Renderer()
    {
        RenderTextureResult result = CreateQueryAndCopySoftwareTexture(colorMod: false);

        await Assert.That(result.WindowCreated).IsTrue();
        await Assert.That(result.RendererCreated).IsTrue();
        await Assert.That(result.SurfaceCreated).IsTrue();
        await SdlAssert.Success(result.FillSurfaceResult, "SDL_FillRect");
        await Assert.That(result.TextureCreated).IsTrue();
        await SdlAssert.Success(result.QueryTextureResult, "SDL_QueryTexture");
        await Assert.That(result.TextureWidth).IsEqualTo(16);
        await Assert.That(result.TextureHeight).IsEqualTo(16);
        await SdlAssert.Success(result.RenderCopyResult, "SDL_RenderCopy");
    }

    [Test]
    [NotInParallel(AbiParallelKeys.Video)]
    [RequiresVideoDummyDriver]
    [Category(AbiCategories.Smoke)]
    [Category(AbiCategories.BehaviorCoverage)]
    [Category(AbiCategories.DummyDriver)]
    [Category(AbiCategories.SdlRender)]
    public async Task SDLSetTextureColorMod_Should_Modulate_And_Copy_Texture_With_Software_Renderer()
    {
        RenderTextureResult result = CreateQueryAndCopySoftwareTexture(colorMod: true);

        await Assert.That(result.WindowCreated).IsTrue();
        await Assert.That(result.RendererCreated).IsTrue();
        await Assert.That(result.SurfaceCreated).IsTrue();
        await Assert.That(result.TextureCreated).IsTrue();
        await SdlAssert.Success(result.SetColorModResult, "SDL_SetTextureColorMod");
        await SdlAssert.Success(result.RenderCopyResult, "SDL_RenderCopy");
    }

    private static int GetDummyRenderDriverCount()
    {
        using SdlEnvironmentScope videoDriver = new("SDL_VIDEODRIVER", "dummy");
        using SdlHintScope renderDriverHint = new("SDL_RENDER_DRIVER", "software");
        using SdlSubsystemScope video = new(SDL_INIT_VIDEO);

        return SDL_GetNumRenderDrivers();
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

    private static unsafe RenderPrimitivesResult DrawSoftwareRendererPrimitives()
    {
        using SdlEnvironmentScope videoDriver = new("SDL_VIDEODRIVER", "dummy");
        using SdlHintScope renderDriverHint = new("SDL_RENDER_DRIVER", "software");
        using SdlSubsystemScope video = new(SDL_INIT_VIDEO);
        using PinnedUtf8 title = SdlUtf8.Pin("Janset SDL2 ABI dummy renderer primitives");

        SDL_Window window = SDL_CreateWindow(title.Pointer, 0, 0, 80, 60, (uint)SDL_WindowFlags.SDL_WINDOW_HIDDEN);
        SDL_Renderer renderer = SDL_Renderer.Null;
        int clearColorResult = -1;
        int clearResult = -1;
        int fillColorResult = -1;
        int fillRectResult = -1;
        int pointColorResult = -1;
        int drawPointResult = -1;
        int lineColorResult = -1;
        int drawLineResult = -1;

        try
        {
            if (window.IsNotNull)
            {
                renderer = SDL_CreateRenderer(window, -1, (uint)SDL_RendererFlags.SDL_RENDERER_SOFTWARE);
                if (renderer.IsNotNull)
                {
                    SDL_Rect rect = new() { x = 10, y = 10, w = 20, h = 15 };
                    clearColorResult = SDL_SetRenderDrawColor(renderer, 0, 0, 0, SDL_ALPHA_OPAQUE);
                    clearResult = SDL_RenderClear(renderer);
                    fillColorResult = SDL_SetRenderDrawColor(renderer, 13, 73, 200, SDL_ALPHA_OPAQUE);
                    fillRectResult = SDL_RenderFillRect(renderer, &rect);
                    pointColorResult = SDL_SetRenderDrawColor(renderer, 200, 0, 100, SDL_ALPHA_OPAQUE);
                    drawPointResult = SDL_RenderDrawPoint(renderer, 3, 5);
                    lineColorResult = SDL_SetRenderDrawColor(renderer, 0, 255, 0, SDL_ALPHA_OPAQUE);
                    drawLineResult = SDL_RenderDrawLine(renderer, 0, 30, 80, 30);
                    SDL_RenderPresent(renderer);
                }
            }

            return new RenderPrimitivesResult(
                window.IsNotNull,
                renderer.IsNotNull,
                clearColorResult,
                clearResult,
                fillColorResult,
                fillRectResult,
                pointColorResult,
                drawPointResult,
                lineColorResult,
                drawLineResult);
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

    private static unsafe RenderTextureResult CreateQueryAndCopySoftwareTexture(bool colorMod)
    {
        using SdlEnvironmentScope videoDriver = new("SDL_VIDEODRIVER", "dummy");
        using SdlHintScope renderDriverHint = new("SDL_RENDER_DRIVER", "software");
        using SdlSubsystemScope video = new(SDL_INIT_VIDEO);
        using PinnedUtf8 title = SdlUtf8.Pin("Janset SDL2 ABI dummy renderer texture");

        SDL_Window window = SDL_CreateWindow(title.Pointer, 0, 0, 64, 64, (uint)SDL_WindowFlags.SDL_WINDOW_HIDDEN);
        SDL_Renderer renderer = SDL_Renderer.Null;
        SDL_Surface* surface = null;
        SDL_Texture texture = SDL_Texture.Null;
        int fillSurfaceResult = -1;
        int queryTextureResult = -1;
        int setColorModResult = 0;
        int renderCopyResult = -1;
        uint format = 0;
        int access = 0;
        int width = 0;
        int height = 0;

        try
        {
            if (window.IsNotNull)
            {
                renderer = SDL_CreateRenderer(window, -1, (uint)SDL_RendererFlags.SDL_RENDERER_SOFTWARE);
                surface = SDL_CreateRGBSurfaceWithFormat(0, 16, 16, 32, (uint)SDL_PixelFormatEnum.SDL_PIXELFORMAT_RGBA8888);
                if (renderer.IsNotNull && surface is not null)
                {
                    uint color = SDL_MapRGBA(surface->format, 40, 80, 120, SDL_ALPHA_OPAQUE);
                    fillSurfaceResult = SDL_FillRect(surface, null, color);
                    texture = SDL_CreateTextureFromSurface(renderer, surface);
                    if (texture.IsNotNull)
                    {
                        SDL_Rect destination = new() { x = 4, y = 4, w = 16, h = 16 };
                        queryTextureResult = SDL_QueryTexture(texture, &format, &access, &width, &height);
                        if (colorMod)
                        {
                            setColorModResult = SDL_SetTextureColorMod(texture, 255, 128, 64);
                        }

                        renderCopyResult = SDL_RenderCopy(renderer, texture, null, &destination);
                        SDL_RenderPresent(renderer);
                    }
                }
            }

            return new RenderTextureResult(
                window.IsNotNull,
                renderer.IsNotNull,
                surface is not null,
                fillSurfaceResult,
                texture.IsNotNull,
                queryTextureResult,
                format,
                access,
                width,
                height,
                setColorModResult,
                renderCopyResult);
        }
        finally
        {
            if (texture.IsNotNull)
            {
                SDL_DestroyTexture(texture);
            }

            if (surface is not null)
            {
                SDL_FreeSurface(surface);
            }

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

    private sealed record RenderPrimitivesResult(
        bool WindowCreated,
        bool RendererCreated,
        int ClearColorResult,
        int ClearResult,
        int FillColorResult,
        int FillRectResult,
        int PointColorResult,
        int DrawPointResult,
        int LineColorResult,
        int DrawLineResult);

    private sealed record RenderTextureResult(
        bool WindowCreated,
        bool RendererCreated,
        bool SurfaceCreated,
        int FillSurfaceResult,
        bool TextureCreated,
        int QueryTextureResult,
        uint TextureFormat,
        int TextureAccess,
        int TextureWidth,
        int TextureHeight,
        int SetColorModResult,
        int RenderCopyResult);
}
