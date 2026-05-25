using Janset.SDL2.AbiTests.Infrastructure;
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

    [Test]
    [NotInParallel(AbiParallelKeys.Video)]
    [RequiresVideoDummyDriver]
    [Category(AbiCategories.UpstreamPort)]
    [Category(AbiCategories.DummyDriver)]
    [Category(AbiCategories.SdlVideo)]
    [UpstreamSdlTest("test/testautomation_video.c", "video_getWindowFlags")]
    public async Task SDLGetWindowFlags_Should_Return_Created_Dummy_Window_Flags()
    {
        WindowFlagsResult result = GetDummyWindowFlags();

        await Assert.That(result.Created).IsTrue();
        await Assert.That((result.ActualFlags & result.ExpectedFlags) == result.ExpectedFlags).IsTrue();
    }

    [Test]
    [NotInParallel(AbiParallelKeys.Video)]
    [RequiresVideoDummyDriver]
    [Category(AbiCategories.UpstreamPort)]
    [Category(AbiCategories.DummyDriver)]
    [Category(AbiCategories.SdlVideo)]
    [UpstreamSdlTest("test/testautomation_video.c", "video_getWindowId")]
    public async Task SDLGetWindowFromID_Should_Return_Dummy_Window_And_Clear_After_Destroy()
    {
        WindowIdResult result = GetDummyWindowByIdAndDestroy();

        await Assert.That(result.WindowCreated).IsTrue();
        await Assert.That(result.Id).IsNotEqualTo(0U);
        await Assert.That(result.FoundBeforeDestroy).IsTrue();
        await Assert.That(result.FoundAfterDestroy).IsFalse();
    }

    [Test]
    [NotInParallel(AbiParallelKeys.Video)]
    [RequiresVideoDummyDriver]
    [Category(AbiCategories.UpstreamPort)]
    [Category(AbiCategories.DummyDriver)]
    [Category(AbiCategories.SdlVideo)]
    [UpstreamSdlTest("test/testautomation_video.c", "video_getWindowPixelFormat")]
    public async Task SDLGetWindowPixelFormat_Should_Return_Known_Format_For_Dummy_Window()
    {
        WindowPixelFormatResult result = GetDummyWindowPixelFormat();

        await Assert.That(result.Created).IsTrue();
        await Assert.That(result.PixelFormat).IsNotEqualTo((uint)SDL_PixelFormatEnum.SDL_PIXELFORMAT_UNKNOWN);
    }

    [Test]
    [NotInParallel(AbiParallelKeys.Video)]
    [RequiresVideoDummyDriver]
    [Category(AbiCategories.UpstreamPort)]
    [Category(AbiCategories.DummyDriver)]
    [Category(AbiCategories.SdlVideo)]
    [UpstreamSdlTest("test/testautomation_video.c", "video_getSetWindowSize")]
    public async Task SDLSetWindowSize_Should_Set_And_Get_Dummy_Window_Size()
    {
        WindowSizeResult result = SetAndGetDummyWindowSize();

        await Assert.That(result.Created).IsTrue();
        await Assert.That(result.Width).IsEqualTo(96);
        await Assert.That(result.Height).IsEqualTo(48);
    }

    [Test]
    [NotInParallel(AbiParallelKeys.Video)]
    [RequiresVideoDummyDriver]
    [Category(AbiCategories.UpstreamPort)]
    [Category(AbiCategories.DummyDriver)]
    [Category(AbiCategories.SdlVideo)]
    [UpstreamSdlTest("test/testautomation_video.c", "video_getWindowSurface")]
    public async Task SDLGetWindowSurface_Should_Update_Destroy_And_Recreate_Dummy_Surface()
    {
        WindowSurfaceResult result = ExerciseDummyWindowSurfaceLifecycle();

        await Assert.That(result.WindowCreated).IsTrue();
        await Assert.That(result.InitialSurfaceCreated).IsTrue();
        await SdlAssert.True(result.HasInitialSurface, "SDL_HasWindowSurface after SDL_GetWindowSurface");
        await SdlAssert.Success(result.UpdateResult, "SDL_UpdateWindowSurface");
        await Assert.That(result.RendererBlockedWhileSurfaceExists).IsTrue();
        await SdlAssert.Success(result.DestroySurfaceResult, "SDL_DestroyWindowSurface");
        await Assert.That(result.HasSurfaceAfterDestroy).IsFalse();
        await Assert.That(result.RendererCreatedAfterSurfaceDestroy).IsTrue();
        await Assert.That(result.SurfaceCreatedAfterRendererDestroy).IsTrue();
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

    private static unsafe WindowFlagsResult GetDummyWindowFlags()
    {
        using SdlEnvironmentScope videoDriver = new("SDL_VIDEODRIVER", "dummy");
        using SdlSubsystemScope video = new(SDL_INIT_VIDEO);

        SDL_WindowFlags expectedFlags = SDL_WindowFlags.SDL_WINDOW_HIDDEN;
        SDL_Window window = CreateDummyWindow("Janset SDL2 ABI dummy flags", 64, 64, expectedFlags);
        try
        {
            uint actualFlags = window.IsNotNull ? SDL_GetWindowFlags(window) : 0;
            return new WindowFlagsResult(window.IsNotNull, (uint)expectedFlags, actualFlags);
        }
        finally
        {
            DestroyWindowIfCreated(window);
        }
    }

    private static unsafe WindowIdResult GetDummyWindowByIdAndDestroy()
    {
        using SdlEnvironmentScope videoDriver = new("SDL_VIDEODRIVER", "dummy");
        using SdlSubsystemScope video = new(SDL_INIT_VIDEO);

        SDL_Window window = CreateDummyWindow("Janset SDL2 ABI dummy id", 64, 64, SDL_WindowFlags.SDL_WINDOW_HIDDEN);
        uint id = 0;
        bool foundBeforeDestroy = false;
        if (window.IsNotNull)
        {
            id = SDL_GetWindowID(window);
            foundBeforeDestroy = SDL_GetWindowFromID(id).Equals(window);
            SDL_DestroyWindow(window);
        }

        bool foundAfterDestroy = id != 0 && SDL_GetWindowFromID(id).IsNotNull;
        return new WindowIdResult(window.IsNotNull, id, foundBeforeDestroy, foundAfterDestroy);
    }

    private static unsafe WindowPixelFormatResult GetDummyWindowPixelFormat()
    {
        using SdlEnvironmentScope videoDriver = new("SDL_VIDEODRIVER", "dummy");
        using SdlSubsystemScope video = new(SDL_INIT_VIDEO);

        SDL_Window window = CreateDummyWindow("Janset SDL2 ABI dummy pixel format", 64, 64, SDL_WindowFlags.SDL_WINDOW_HIDDEN);
        try
        {
            uint pixelFormat = window.IsNotNull ? SDL_GetWindowPixelFormat(window) : 0;
            return new WindowPixelFormatResult(window.IsNotNull, pixelFormat);
        }
        finally
        {
            DestroyWindowIfCreated(window);
        }
    }

    private static unsafe WindowSizeResult SetAndGetDummyWindowSize()
    {
        using SdlEnvironmentScope videoDriver = new("SDL_VIDEODRIVER", "dummy");
        using SdlSubsystemScope video = new(SDL_INIT_VIDEO);

        SDL_Window window = CreateDummyWindow("Janset SDL2 ABI dummy size", 32, 32, SDL_WindowFlags.SDL_WINDOW_HIDDEN);
        int width = 0;
        int height = 0;
        try
        {
            if (window.IsNotNull)
            {
                SDL_SetWindowSize(window, 96, 48);
                SDL_GetWindowSize(window, &width, &height);
            }

            return new WindowSizeResult(window.IsNotNull, width, height);
        }
        finally
        {
            DestroyWindowIfCreated(window);
        }
    }

    private static unsafe WindowSurfaceResult ExerciseDummyWindowSurfaceLifecycle()
    {
        using SdlEnvironmentScope videoDriver = new("SDL_VIDEODRIVER", "dummy");
        using SdlHintScope framebufferAcceleration = new("SDL_FRAMEBUFFER_ACCELERATION", "1");
        using SdlSubsystemScope video = new(SDL_INIT_VIDEO);

        SDL_Window window = CreateDummyWindow("Janset SDL2 ABI dummy surface", 64, 64, SDL_WindowFlags.SDL_WINDOW_HIDDEN);
        SDL_Renderer renderer = SDL_Renderer.Null;
        SDL_Renderer blockedRenderer = SDL_Renderer.Null;
        try
        {
            SDL_Surface* surface = window.IsNotNull ? SDL_GetWindowSurface(window) : null;
            SDL_bool hasInitialSurface = window.IsNotNull ? SDL_HasWindowSurface(window) : SDL_bool.SDL_FALSE;
            int updateResult = window.IsNotNull ? SDL_UpdateWindowSurface(window) : -1;
            blockedRenderer = window.IsNotNull ? SDL_CreateRenderer(window, -1, (uint)SDL_RendererFlags.SDL_RENDERER_SOFTWARE) : SDL_Renderer.Null;
            int destroySurfaceResult = window.IsNotNull ? SDL_DestroyWindowSurface(window) : -1;
            bool hasSurfaceAfterDestroy = window.IsNotNull && SDL_HasWindowSurface(window) == SDL_bool.SDL_TRUE;
            renderer = window.IsNotNull ? SDL_CreateRenderer(window, -1, (uint)SDL_RendererFlags.SDL_RENDERER_SOFTWARE) : SDL_Renderer.Null;
            bool rendererCreatedAfterSurfaceDestroy = renderer.IsNotNull;
            if (renderer.IsNotNull)
            {
                SDL_DestroyRenderer(renderer);
                renderer = SDL_Renderer.Null;
            }

            SDL_Surface* recreatedSurface = window.IsNotNull ? SDL_GetWindowSurface(window) : null;
            return new WindowSurfaceResult(
                window.IsNotNull,
                surface is not null,
                hasInitialSurface,
                updateResult,
                blockedRenderer.Equals(SDL_Renderer.Null),
                destroySurfaceResult,
                hasSurfaceAfterDestroy,
                rendererCreatedAfterSurfaceDestroy,
                recreatedSurface is not null);
        }
        finally
        {
            if (blockedRenderer.IsNotNull)
            {
                SDL_DestroyRenderer(blockedRenderer);
            }

            if (renderer.IsNotNull)
            {
                SDL_DestroyRenderer(renderer);
            }

            DestroyWindowIfCreated(window);
        }
    }

    private static unsafe SDL_Window CreateDummyWindow(string title, int width, int height, SDL_WindowFlags flags)
    {
        using PinnedUtf8 pinnedTitle = SdlUtf8.Pin(title);
        return SDL_CreateWindow(pinnedTitle.Pointer, 0, 0, width, height, (uint)flags);
    }

    private static void DestroyWindowIfCreated(SDL_Window window)
    {
        if (window.IsNotNull)
        {
            SDL_DestroyWindow(window);
        }
    }

    private sealed record WindowResult(bool Created);

    private sealed record WindowFlagsResult(bool Created, uint ExpectedFlags, uint ActualFlags);

    private sealed record WindowIdResult(bool WindowCreated, uint Id, bool FoundBeforeDestroy, bool FoundAfterDestroy);

    private sealed record WindowPixelFormatResult(bool Created, uint PixelFormat);

    private sealed record WindowSizeResult(bool Created, int Width, int Height);

    private sealed record WindowSurfaceResult(
        bool WindowCreated,
        bool InitialSurfaceCreated,
        SDL_bool HasInitialSurface,
        int UpdateResult,
        bool RendererBlockedWhileSurfaceExists,
        int DestroySurfaceResult,
        bool HasSurfaceAfterDestroy,
        bool RendererCreatedAfterSurfaceDestroy,
        bool SurfaceCreatedAfterRendererDestroy);
}
