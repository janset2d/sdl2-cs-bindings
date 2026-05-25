using Janset.SDL2.AbiTests.Infrastructure;
using Janset.SDL2.AbiTests.Infrastructure.Assets;
using Janset.SDL2.AbiTests.Infrastructure.Classification;
using Janset.SDL2.AbiTests.Infrastructure.Sdl;
using SDL2;
using static SDL2.SDLNative;

namespace Janset.SDL2.AbiTests.Upstream.Assets;

public sealed class SurfaceAbiTests
{
    [Test]
    [Category(AbiCategories.UpstreamPort)]
    [Category(AbiCategories.Mechanical)]
    [Category(AbiCategories.Assets)]
    [Category(AbiCategories.SdlSurface)]
    [UpstreamSdlTest("test/testautomation_surface.c", "surface_testSaveLoadBitmap")]
    public async Task SDLLoadSaveBmpRw_Should_Load_And_Save_Upstream_Sample_Bmp()
    {
        using AbiTempDirectory temp = new();

        string samplePath = UpstreamSdlAsset.Path("sample.bmp");
        string savedPath = temp.GetFilePath("saved-sample.bmp");

        SurfaceResult result = LoadAndSaveBmp(samplePath, savedPath);

        await Assert.That(result.Width).IsGreaterThan(0);
        await Assert.That(result.Height).IsGreaterThan(0);
        await Assert.That(result.Pitch).IsGreaterThan(0);
        await SdlAssert.Success(result.SaveResult, "SDL_SaveBMP_RW");
        await Assert.That(File.Exists(savedPath)).IsTrue();
        await Assert.That(new FileInfo(savedPath).Length).IsGreaterThan(0L);
        await Assert.That(result.ReloadedWidth).IsEqualTo(result.Width);
        await Assert.That(result.ReloadedHeight).IsEqualTo(result.Height);
    }

    [Test]
    [Category(AbiCategories.Smoke)]
    [Category(AbiCategories.Mechanical)]
    [Category(AbiCategories.Assets)]
    [Category(AbiCategories.SdlSurface)]
    public async Task SDLLoadBmpRw_Should_Return_Null_When_Bmp_File_Does_Not_Exist()
    {
        using AbiTempDirectory temp = new();

        LoadFailureResult result = TryLoadMissingBmp(temp.GetFilePath("nonexistent.bmp"));

        await Assert.That(result.RwopsWasNull).IsTrue();
        await Assert.That(result.FileOpenError).IsNotEmpty();
        await Assert.That(result.SurfaceWasNull).IsTrue();
    }

    [Test]
    [Category(AbiCategories.HeaderCoverage)]
    [Category(AbiCategories.Mechanical)]
    [Category(AbiCategories.Assets)]
    [Category(AbiCategories.SdlSurface)]
    public async Task SDLConvertSurface_Should_Preserve_Dimensions_And_Use_Target_Format()
    {
        ConversionResult result = ConvertSampleBmpToFormat(UpstreamSdlAsset.Path("sample.bmp"));

        await Assert.That(result.SourceWidth).IsGreaterThan(0);
        await Assert.That(result.SourceHeight).IsGreaterThan(0);
        await Assert.That(result.ConvertedWidth).IsEqualTo(result.SourceWidth);
        await Assert.That(result.ConvertedHeight).IsEqualTo(result.SourceHeight);
        await Assert.That(result.ConvertedPitch).IsGreaterThan(0);
        await Assert.That(result.ConvertedFormat).IsEqualTo((uint)SDL_PixelFormatEnum.SDL_PIXELFORMAT_ARGB8888);
    }

    [Test]
    [NotInParallel(AbiParallelKeys.Error)]
    [Category(AbiCategories.UpstreamPort)]
    [Category(AbiCategories.BehaviorCoverage)]
    [Category(AbiCategories.GlobalState)]
    [Category(AbiCategories.SdlSurface)]
    [UpstreamSdlTest("test/testautomation_surface.c", "surface_testOverflow")]
    public async Task SDLCreateRgbSurface_Should_Reject_Invalid_Dimensions_And_Pitch()
    {
        OverflowResult result = TestInvalidSurfaceCreation();

        await Assert.That(result.NegativeWidthFailures).IsEqualTo(3);
        await Assert.That(result.NegativeHeightFailures).IsEqualTo(3);
        await Assert.That(result.NegativePitchFailures).IsEqualTo(2);
        await Assert.That(result.PitchTooSmallFailures).IsEqualTo(10);
        await Assert.That(result.ValidPitchAllocations).IsEqualTo(16);
        await Assert.That(result.ErrorAfterCleanup).IsEmpty();
    }

    [Test]
    [Category(AbiCategories.HeaderCoverage)]
    [Category(AbiCategories.BehaviorCoverage)]
    [Category(AbiCategories.Pure)]
    [Category(AbiCategories.SdlSurface)]
    public async Task SDLSetSurfaceBlendMode_And_SDLUpperBlit_Should_Copy_Source_Pixels_When_Blend_None()
    {
        BlitResult result = BlitWithBlendNone();

        await SdlAssert.Success(result.SetBlendResult, "SDL_SetSurfaceBlendMode");
        await SdlAssert.Success(result.GetBlendResult, "SDL_GetSurfaceBlendMode");
        await Assert.That(result.BlendMode).IsEqualTo(SDL_BlendMode.SDL_BLENDMODE_NONE);
        await SdlAssert.Success(result.BlitResultCode, "SDL_UpperBlit");
        await Assert.That(result.DestinationPixel).IsEqualTo(result.SourcePixel);
    }

    private static unsafe SurfaceResult LoadAndSaveBmp(string samplePath, string savedPath)
    {
        SDL_Surface* surface = null;
        SDL_Surface* reloaded = null;

        try
        {
            surface = SdlMacro.LoadBmp(samplePath);
            int saveResult = SdlMacro.SaveBmp(surface, savedPath);
            if (saveResult != 0)
            {
                return new SurfaceResult(surface->w, surface->h, surface->pitch, saveResult, 0, 0);
            }

            reloaded = SdlMacro.LoadBmp(savedPath);

            return new SurfaceResult(surface->w, surface->h, surface->pitch, saveResult, reloaded->w, reloaded->h);
        }
        finally
        {
            if (reloaded is not null)
            {
                SDL_FreeSurface(reloaded);
            }

            if (surface is not null)
            {
                SDL_FreeSurface(surface);
            }
        }
    }

    private static unsafe LoadFailureResult TryLoadMissingBmp(string missingPath)
    {
        using PinnedUtf8 pinnedPath = SdlUtf8.Pin(missingPath);
        using PinnedUtf8 readMode = SdlUtf8.Pin("rb");

        SdlError.Clear();

        SDL_RWops source = SDL_RWFromFile(pinnedPath.Pointer, readMode.Pointer);
        string fileOpenError = SdlError.Current;

        SDL_Surface* surface = SDL_LoadBMP_RW(source, 1);
        if (surface is not null)
        {
            SDL_FreeSurface(surface);
        }

        return new LoadFailureResult(source.IsNull, fileOpenError, surface is null);
    }

    private static unsafe ConversionResult ConvertSampleBmpToFormat(string samplePath)
    {
        SDL_Surface* source = null;
        SDL_Surface* converted = null;

        try
        {
            source = SdlMacro.LoadBmp(samplePath);
            converted = SDL_ConvertSurfaceFormat(source, (uint)SDL_PixelFormatEnum.SDL_PIXELFORMAT_ARGB8888, 0);
            if (converted is null)
            {
                throw new InvalidOperationException($"SDL_ConvertSurfaceFormat failed: {SdlError.Current}");
            }

            return new ConversionResult(source->w, source->h, converted->w, converted->h, converted->pitch, converted->format->format);
        }
        finally
        {
            if (converted is not null)
            {
                SDL_FreeSurface(converted);
            }

            if (source is not null)
            {
                SDL_FreeSurface(source);
            }
        }
    }

    private static unsafe OverflowResult TestInvalidSurfaceCreation()
    {
        byte[] buffer = new byte[1024];
        SdlError.Clear();

        try
        {
            fixed (byte* bufferPointer = buffer)
            {
                OverflowResult result = new(
                    CountNullSurfaces(
                        SDL_CreateRGBSurfaceWithFormat(0, -3, 100, 8, (uint)SDL_PixelFormatEnum.SDL_PIXELFORMAT_INDEX8),
                        SDL_CreateRGBSurfaceWithFormatFrom((nint)bufferPointer, -1, 1, 8, 4, (uint)SDL_PixelFormatEnum.SDL_PIXELFORMAT_INDEX8),
                        SDL_CreateRGBSurfaceFrom((nint)bufferPointer, -1, 1, 32, 4, 0xFF000000, 0x00FF0000, 0x0000FF00, 0x000000FF)),
                    CountNullSurfaces(
                        SDL_CreateRGBSurfaceWithFormat(0, 100, -3, 8, (uint)SDL_PixelFormatEnum.SDL_PIXELFORMAT_INDEX8),
                        SDL_CreateRGBSurfaceWithFormatFrom((nint)bufferPointer, 1, -1, 8, 4, (uint)SDL_PixelFormatEnum.SDL_PIXELFORMAT_INDEX8),
                        SDL_CreateRGBSurfaceFrom((nint)bufferPointer, 1, -1, 32, 4, 0xFF000000, 0x00FF0000, 0x0000FF00, 0x000000FF)),
                    CountNullSurfaces(
                        SDL_CreateRGBSurfaceWithFormatFrom((nint)bufferPointer, 4, 1, 8, -1, (uint)SDL_PixelFormatEnum.SDL_PIXELFORMAT_INDEX8),
                        SDL_CreateRGBSurfaceFrom((nint)bufferPointer, 1, 1, 32, -1, 0xFF000000, 0x00FF0000, 0x0000FF00, 0x000000FF)),
                    CountNullSurfaces(
                        SDL_CreateRGBSurfaceWithFormatFrom((nint)bufferPointer, 7, 1, 4, 3, (uint)SDL_PixelFormatEnum.SDL_PIXELFORMAT_INDEX4LSB),
                        SDL_CreateRGBSurfaceFrom((nint)bufferPointer, 7, 1, 4, 3, 0, 0, 0, 0),
                        SDL_CreateRGBSurfaceWithFormatFrom((nint)bufferPointer, 13, 1, 2, 3, (uint)SDL_PixelFormatEnum.SDL_PIXELFORMAT_INDEX2LSB),
                        SDL_CreateRGBSurfaceFrom((nint)bufferPointer, 13, 1, 2, 3, 0, 0, 0, 0),
                        SDL_CreateRGBSurfaceWithFormatFrom((nint)bufferPointer, 17, 1, 1, 2, (uint)SDL_PixelFormatEnum.SDL_PIXELFORMAT_INDEX1LSB),
                        SDL_CreateRGBSurfaceFrom((nint)bufferPointer, 17, 1, 1, 2, 0, 0, 0, 0),
                        SDL_CreateRGBSurfaceWithFormatFrom((nint)bufferPointer, 6, 1, 8, 5, (uint)SDL_PixelFormatEnum.SDL_PIXELFORMAT_RGB332),
                        SDL_CreateRGBSurfaceFrom((nint)bufferPointer, 6, 1, 8, 5, 0, 0, 0, 0),
                        SDL_CreateRGBSurfaceWithFormatFrom((nint)bufferPointer, 4, 1, 15, 6, (uint)SDL_PixelFormatEnum.SDL_PIXELFORMAT_RGB555),
                        SDL_CreateRGBSurfaceFrom((nint)bufferPointer, 4, 1, 15, 6, 0, 0, 0, 0)),
                    CountAllocatedSurfaces(
                        SDL_CreateRGBSurfaceWithFormatFrom((nint)bufferPointer, 6, 1, 4, 3, (uint)SDL_PixelFormatEnum.SDL_PIXELFORMAT_INDEX4LSB),
                        SDL_CreateRGBSurfaceFrom((nint)bufferPointer, 6, 1, 4, 3, 0, 0, 0, 0),
                        SDL_CreateRGBSurfaceWithFormatFrom((nint)bufferPointer, 7, 1, 4, 4, (uint)SDL_PixelFormatEnum.SDL_PIXELFORMAT_INDEX4LSB),
                        SDL_CreateRGBSurfaceFrom((nint)bufferPointer, 7, 1, 4, 4, 0, 0, 0, 0),
                        SDL_CreateRGBSurfaceWithFormatFrom((nint)bufferPointer, 12, 1, 2, 3, (uint)SDL_PixelFormatEnum.SDL_PIXELFORMAT_INDEX2LSB),
                        SDL_CreateRGBSurfaceFrom((nint)bufferPointer, 12, 1, 2, 3, 0, 0, 0, 0),
                        SDL_CreateRGBSurfaceWithFormatFrom((nint)bufferPointer, 13, 1, 2, 4, (uint)SDL_PixelFormatEnum.SDL_PIXELFORMAT_INDEX2LSB),
                        SDL_CreateRGBSurfaceFrom((nint)bufferPointer, 13, 1, 2, 4, 0, 0, 0, 0),
                        SDL_CreateRGBSurfaceWithFormatFrom((nint)bufferPointer, 16, 1, 1, 2, (uint)SDL_PixelFormatEnum.SDL_PIXELFORMAT_INDEX1LSB),
                        SDL_CreateRGBSurfaceFrom((nint)bufferPointer, 16, 1, 1, 2, 0, 0, 0, 0),
                        SDL_CreateRGBSurfaceWithFormatFrom((nint)bufferPointer, 17, 1, 1, 3, (uint)SDL_PixelFormatEnum.SDL_PIXELFORMAT_INDEX1LSB),
                        SDL_CreateRGBSurfaceFrom((nint)bufferPointer, 17, 1, 1, 3, 0, 0, 0, 0),
                        SDL_CreateRGBSurfaceWithFormatFrom((nint)bufferPointer, 5, 1, 8, 5, (uint)SDL_PixelFormatEnum.SDL_PIXELFORMAT_RGB332),
                        SDL_CreateRGBSurfaceFrom((nint)bufferPointer, 5, 1, 8, 5, 0, 0, 0, 0),
                        SDL_CreateRGBSurfaceWithFormatFrom((nint)bufferPointer, 3, 1, 15, 6, (uint)SDL_PixelFormatEnum.SDL_PIXELFORMAT_RGB555),
                        SDL_CreateRGBSurfaceFrom((nint)bufferPointer, 3, 1, 15, 6, 0, 0, 0, 0)),
                    string.Empty);

                SdlError.Clear();
                return result with { ErrorAfterCleanup = SdlError.Current };
            }
        }
        finally
        {
            SdlError.Clear();
        }
    }

    private static unsafe int CountNullSurfaces(params SDL_Surface*[] surfaces)
    {
        int count = 0;
        foreach (SDL_Surface* surface in surfaces)
        {
            if (surface is null)
            {
                count++;
            }
            else
            {
                SDL_FreeSurface(surface);
            }
        }

        return count;
    }

    private static unsafe int CountAllocatedSurfaces(params SDL_Surface*[] surfaces)
    {
        int count = 0;
        foreach (SDL_Surface* surface in surfaces)
        {
            if (surface is not null)
            {
                count++;
                SDL_FreeSurface(surface);
            }
        }

        return count;
    }

    private static unsafe BlitResult BlitWithBlendNone()
    {
        SDL_Surface* source = null;
        SDL_Surface* destination = null;

        try
        {
            source = CreateArgbSurface(2, 2);
            destination = CreateArgbSurface(2, 2);
            uint sourcePixel = SDL_MapRGBA(source->format, 11, 22, 33, 44);
            uint destinationPixel = SDL_MapRGBA(destination->format, 200, 201, 202, 203);

            int fillSourceResult = SDL_FillRect(source, null, sourcePixel);
            if (fillSourceResult != 0)
            {
                throw new InvalidOperationException($"SDL_FillRect source failed: {SdlError.Current}");
            }

            int fillDestinationResult = SDL_FillRect(destination, null, destinationPixel);
            if (fillDestinationResult != 0)
            {
                throw new InvalidOperationException($"SDL_FillRect destination failed: {SdlError.Current}");
            }

            int setBlendResult = SDL_SetSurfaceBlendMode(source, SDL_BlendMode.SDL_BLENDMODE_NONE);
            SDL_BlendMode actualBlendMode = 0;
            int getBlendResult = SDL_GetSurfaceBlendMode(source, &actualBlendMode);
            int blitResult = SDL_UpperBlit(source, null, destination, null);

            uint copiedPixel = ReadFirstPixel(destination);
            return new BlitResult(setBlendResult, getBlendResult, actualBlendMode, blitResult, sourcePixel, copiedPixel);
        }
        finally
        {
            if (destination is not null)
            {
                SDL_FreeSurface(destination);
            }

            if (source is not null)
            {
                SDL_FreeSurface(source);
            }
        }
    }

    private static unsafe SDL_Surface* CreateArgbSurface(int width, int height)
    {
        SDL_Surface* surface = SDL_CreateRGBSurfaceWithFormat(0, width, height, 32, (uint)SDL_PixelFormatEnum.SDL_PIXELFORMAT_ARGB8888);
        if (surface is null)
        {
            throw new InvalidOperationException($"SDL_CreateRGBSurfaceWithFormat failed: {SdlError.Current}");
        }

        return surface;
    }

    private static unsafe uint ReadFirstPixel(SDL_Surface* surface)
    {
        int lockResult = SDL_LockSurface(surface);
        if (lockResult != 0)
        {
            throw new InvalidOperationException($"SDL_LockSurface failed: {SdlError.Current}");
        }

        try
        {
            return *(uint*)surface->pixels;
        }
        finally
        {
            SDL_UnlockSurface(surface);
        }
    }

    private sealed record SurfaceResult(int Width, int Height, int Pitch, int SaveResult, int ReloadedWidth, int ReloadedHeight);

    private sealed record LoadFailureResult(bool RwopsWasNull, string FileOpenError, bool SurfaceWasNull);

    private sealed record ConversionResult(
        int SourceWidth,
        int SourceHeight,
        int ConvertedWidth,
        int ConvertedHeight,
        int ConvertedPitch,
        uint ConvertedFormat);

    private sealed record OverflowResult(
        int NegativeWidthFailures,
        int NegativeHeightFailures,
        int NegativePitchFailures,
        int PitchTooSmallFailures,
        int ValidPitchAllocations,
        string ErrorAfterCleanup);

    private sealed record BlitResult(
        int SetBlendResult,
        int GetBlendResult,
        SDL_BlendMode BlendMode,
        int BlitResultCode,
        uint SourcePixel,
        uint DestinationPixel);
}
