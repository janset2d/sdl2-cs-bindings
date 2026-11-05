using Janset.SDL2.AbiTests.Infrastructure.Classification;
using Janset.SDL2.AbiTests.Infrastructure.Sdl;
using SDL2;
using static SDL2.SDLNative;

namespace Janset.SDL2.AbiTests.Upstream.Pure;

public sealed class PixelsAbiTests
{
    private const uint InvalidPacked32Abgr1010103 = 0x16692004;
    private const uint InvalidPacked32Abgr1010104 = 0x166A2004;

    [Test]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_INDEX1LSB)]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_INDEX1MSB)]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_INDEX2LSB)]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_INDEX2MSB)]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_INDEX4LSB)]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_INDEX4MSB)]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_INDEX8)]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_RGB332)]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_RGB444)]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_BGR444)]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_RGB555)]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_BGR555)]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_ARGB4444)]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_RGBA4444)]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_ABGR4444)]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_BGRA4444)]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_ARGB1555)]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_RGBA5551)]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_ABGR1555)]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_BGRA5551)]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_RGB565)]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_BGR565)]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_RGB24)]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_BGR24)]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_RGB888)]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_RGBX8888)]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_BGR888)]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_BGRX8888)]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_ARGB8888)]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_RGBA8888)]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_ABGR8888)]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_BGRA8888)]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_ARGB2101010)]
    [Category(AbiCategories.UpstreamPort)]
    [Category(AbiCategories.Mechanical)]
    [Category(AbiCategories.Pure)]
    [Category(AbiCategories.SdlPixels)]
    [UpstreamSdlTest("test/testautomation_pixels.c", "pixels_allocFreeFormat")]
    public async Task SDLAllocFormat_Should_Return_Format_Metadata_For_Rgb_Format(SDL_PixelFormatEnum format)
    {
        PixelFormatSnapshot snapshot = AllocateFormat((uint)format);

        await Assert.That(snapshot.Format).IsEqualTo((uint)format);
        await Assert.That(snapshot.BitsPerPixel).IsGreaterThan((byte)0);
        await Assert.That(snapshot.BytesPerPixel).IsGreaterThan((byte)0);
    }

    [Test]
    [Category(AbiCategories.UpstreamPort)]
    [Category(AbiCategories.Mechanical)]
    [Category(AbiCategories.Pure)]
    [Category(AbiCategories.SdlPixels)]
    [UpstreamSdlTest("test/testautomation_pixels.c", "pixels_allocFreeFormat")]
    public async Task SDLAllocFormat_Should_Return_Empty_Metadata_For_Unknown_Format()
    {
        PixelFormatSnapshot snapshot = AllocateFormat((uint)SDL_PixelFormatEnum.SDL_PIXELFORMAT_UNKNOWN);

        await Assert.That(snapshot.Format).IsEqualTo((uint)SDL_PixelFormatEnum.SDL_PIXELFORMAT_UNKNOWN);
        await Assert.That(snapshot.BitsPerPixel).IsEqualTo((byte)0);
        await Assert.That(snapshot.BytesPerPixel).IsEqualTo((byte)0);
        await Assert.That(snapshot.CombinedMasks).IsEqualTo(0u);
    }

    [Test]
    [Arguments(InvalidPacked32Abgr1010103)]
    [Arguments(InvalidPacked32Abgr1010104)]
    [Category(AbiCategories.UpstreamPort)]
    [Category(AbiCategories.Mechanical)]
    [Category(AbiCategories.Pure)]
    [Category(AbiCategories.SdlPixels)]
    [UpstreamSdlTest("test/testautomation_pixels.c", "pixels_allocFreeFormat")]
    public async Task SDLAllocFormat_Should_Return_Null_And_Set_Error_For_Invalid_Format(uint format)
    {
        AllocationFailureSnapshot snapshot = TryAllocateFormat(format);

        await Assert.That(snapshot.Allocated).IsFalse();
        await Assert.That(snapshot.Error).IsNotEmpty();
    }

    [Test]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_UNKNOWN, "SDL_PIXELFORMAT_UNKNOWN")]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_INDEX1LSB, "SDL_PIXELFORMAT_INDEX1LSB")]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_INDEX1MSB, "SDL_PIXELFORMAT_INDEX1MSB")]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_INDEX2LSB, "SDL_PIXELFORMAT_INDEX2LSB")]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_INDEX2MSB, "SDL_PIXELFORMAT_INDEX2MSB")]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_INDEX4LSB, "SDL_PIXELFORMAT_INDEX4LSB")]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_INDEX4MSB, "SDL_PIXELFORMAT_INDEX4MSB")]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_INDEX8, "SDL_PIXELFORMAT_INDEX8")]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_RGB332, "SDL_PIXELFORMAT_RGB332")]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_RGB444, "SDL_PIXELFORMAT_RGB444")]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_BGR444, "SDL_PIXELFORMAT_BGR444")]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_RGB555, "SDL_PIXELFORMAT_RGB555")]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_BGR555, "SDL_PIXELFORMAT_BGR555")]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_ARGB4444, "SDL_PIXELFORMAT_ARGB4444")]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_RGBA4444, "SDL_PIXELFORMAT_RGBA4444")]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_ABGR4444, "SDL_PIXELFORMAT_ABGR4444")]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_BGRA4444, "SDL_PIXELFORMAT_BGRA4444")]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_ARGB1555, "SDL_PIXELFORMAT_ARGB1555")]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_RGBA5551, "SDL_PIXELFORMAT_RGBA5551")]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_ABGR1555, "SDL_PIXELFORMAT_ABGR1555")]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_BGRA5551, "SDL_PIXELFORMAT_BGRA5551")]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_RGB565, "SDL_PIXELFORMAT_RGB565")]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_BGR565, "SDL_PIXELFORMAT_BGR565")]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_RGB24, "SDL_PIXELFORMAT_RGB24")]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_BGR24, "SDL_PIXELFORMAT_BGR24")]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_RGB888, "SDL_PIXELFORMAT_RGB888")]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_RGBX8888, "SDL_PIXELFORMAT_RGBX8888")]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_BGR888, "SDL_PIXELFORMAT_BGR888")]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_BGRX8888, "SDL_PIXELFORMAT_BGRX8888")]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_ARGB8888, "SDL_PIXELFORMAT_ARGB8888")]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_RGBA8888, "SDL_PIXELFORMAT_RGBA8888")]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_ABGR8888, "SDL_PIXELFORMAT_ABGR8888")]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_BGRA8888, "SDL_PIXELFORMAT_BGRA8888")]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_ARGB2101010, "SDL_PIXELFORMAT_ARGB2101010")]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_YV12, "SDL_PIXELFORMAT_YV12")]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_IYUV, "SDL_PIXELFORMAT_IYUV")]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_YUY2, "SDL_PIXELFORMAT_YUY2")]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_UYVY, "SDL_PIXELFORMAT_UYVY")]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_YVYU, "SDL_PIXELFORMAT_YVYU")]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_NV12, "SDL_PIXELFORMAT_NV12")]
    [Arguments(SDL_PixelFormatEnum.SDL_PIXELFORMAT_NV21, "SDL_PIXELFORMAT_NV21")]
    [Category(AbiCategories.UpstreamPort)]
    [Category(AbiCategories.BehaviorCoverage)]
    [Category(AbiCategories.Pure)]
    [Category(AbiCategories.SdlPixels)]
    [UpstreamSdlTest("test/testautomation_pixels.c", "pixels_getPixelFormatName")]
    public async Task SDLGetPixelFormatName_Should_Return_Expected_Format_Name(SDL_PixelFormatEnum format, string expectedName)
    {
        string name = GetPixelFormatName((uint)format);

        await Assert.That(name).IsEqualTo(expectedName);
    }

    [Test]
    [Arguments(InvalidPacked32Abgr1010103)]
    [Arguments(InvalidPacked32Abgr1010104)]
    [Category(AbiCategories.UpstreamPort)]
    [Category(AbiCategories.BehaviorCoverage)]
    [Category(AbiCategories.Pure)]
    [Category(AbiCategories.SdlPixels)]
    [UpstreamSdlTest("test/testautomation_pixels.c", "pixels_getPixelFormatName")]
    public async Task SDLGetPixelFormatName_Should_Return_Unknown_And_Not_Set_Error_For_Invalid_Format(uint format)
    {
        SdlError.Clear();

        string name = GetPixelFormatName(format);

        await Assert.That(name).IsEqualTo("SDL_PIXELFORMAT_UNKNOWN");
        await Assert.That(SdlError.Current).IsEmpty();
    }

    [Test]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(8)]
    [Category(AbiCategories.UpstreamPort)]
    [Category(AbiCategories.Mechanical)]
    [Category(AbiCategories.Pure)]
    [Category(AbiCategories.SdlPixels)]
    [UpstreamSdlTest("test/testautomation_pixels.c", "pixels_allocFreePalette")]
    public async Task SDLAllocPalette_Should_Return_Palette_With_White_Colors(int ncolors)
    {
        PaletteSnapshot snapshot = AllocatePalette(ncolors);

        await Assert.That(snapshot.ColorCount).IsEqualTo(ncolors);
        await Assert.That(snapshot.ColorsPointerIsNonNull).IsTrue();
        await Assert.That(snapshot.Red).IsEqualTo((byte)255);
        await Assert.That(snapshot.Green).IsEqualTo((byte)255);
        await Assert.That(snapshot.Blue).IsEqualTo((byte)255);
    }

    [Test]
    [Arguments(0)]
    [Arguments(-1)]
    [Arguments(-2)]
    [Category(AbiCategories.UpstreamPort)]
    [Category(AbiCategories.Mechanical)]
    [Category(AbiCategories.Pure)]
    [Category(AbiCategories.SdlPixels)]
    [UpstreamSdlTest("test/testautomation_pixels.c", "pixels_allocFreePalette")]
    public async Task SDLAllocPalette_Should_Return_Null_And_Set_Error_For_Invalid_Color_Count(int ncolors)
    {
        AllocationFailureSnapshot snapshot = TryAllocatePalette(ncolors);

        await Assert.That(snapshot.Allocated).IsFalse();
        await Assert.That(snapshot.Error).IsEqualTo("Parameter 'ncolors' is invalid");
    }

    [Test]
    [Category(AbiCategories.UpstreamPort)]
    [Category(AbiCategories.BehaviorCoverage)]
    [Category(AbiCategories.Pure)]
    [Category(AbiCategories.SdlPixels)]
    [UpstreamSdlTest("test/testautomation_pixels.c", "pixels_calcGammaRamp")]
    public async Task SDLCalculateGammaRamp_Should_Write_Black_Ramp_For_Zero_Gamma()
    {
        GammaRampSnapshot snapshot = CalculateGammaRamp(0.0f);

        await Assert.That(snapshot.ChangedCount).IsGreaterThan(250);
        await Assert.That(snapshot.Sample).IsEqualTo((ushort)0);
    }

    [Test]
    [Category(AbiCategories.UpstreamPort)]
    [Category(AbiCategories.BehaviorCoverage)]
    [Category(AbiCategories.Pure)]
    [Category(AbiCategories.SdlPixels)]
    [UpstreamSdlTest("test/testautomation_pixels.c", "pixels_calcGammaRamp")]
    public async Task SDLCalculateGammaRamp_Should_Write_Identity_Ramp_For_One_Gamma()
    {
        const int sampleIndex = 128;

        GammaRampSnapshot snapshot = CalculateGammaRamp(1.0f, sampleIndex);

        await Assert.That(snapshot.ChangedCount).IsGreaterThan(250);
        await Assert.That(snapshot.Sample).IsEqualTo((ushort)((sampleIndex << 8) | sampleIndex));
    }

    [Test]
    [Category(AbiCategories.UpstreamPort)]
    [Category(AbiCategories.BehaviorCoverage)]
    [Category(AbiCategories.Pure)]
    [Category(AbiCategories.SdlPixels)]
    [UpstreamSdlTest("test/testautomation_pixels.c", "pixels_calcGammaRamp")]
    public async Task SDLCalculateGammaRamp_Should_Set_Error_And_Leave_Ramp_Unchanged_For_Negative_Gamma()
    {
        GammaRampFailureSnapshot snapshot = CalculateInvalidGammaRamp();

        await Assert.That(snapshot.ChangedCount).IsEqualTo(0);
        await Assert.That(snapshot.Error).IsEqualTo("Parameter 'gamma' is invalid");
    }

    [Test]
    [Category(AbiCategories.UpstreamPort)]
    [Category(AbiCategories.BehaviorCoverage)]
    [Category(AbiCategories.Pure)]
    [Category(AbiCategories.SdlPixels)]
    [UpstreamSdlTest("test/testautomation_pixels.c", "pixels_calcGammaRamp")]
    public async Task SDLCalculateGammaRamp_Should_Set_Error_For_Null_Ramp()
    {
        SdlError.Clear();

        unsafe
        {
            SDL_CalculateGammaRamp(0.5f, null);
        }

        await Assert.That(SdlError.Current).IsEqualTo("Parameter 'ramp' is invalid");
    }

    private static unsafe PixelFormatSnapshot AllocateFormat(uint format)
    {
        SDL_PixelFormat* allocated = SDL_AllocFormat(format);
        if (allocated is null)
        {
            throw new InvalidOperationException($"SDL_AllocFormat failed: {SdlError.Current}");
        }

        try
        {
            return new PixelFormatSnapshot(
                allocated->format,
                allocated->BitsPerPixel,
                allocated->BytesPerPixel,
                allocated->Rmask | allocated->Gmask | allocated->Bmask | allocated->Amask);
        }
        finally
        {
            SDL_FreeFormat(allocated);
        }
    }

    private static unsafe AllocationFailureSnapshot TryAllocateFormat(uint format)
    {
        SdlError.Clear();

        SDL_PixelFormat* allocated = SDL_AllocFormat(format);
        if (allocated is not null)
        {
            SDL_FreeFormat(allocated);
        }

        return new AllocationFailureSnapshot(allocated is not null, SdlError.Current);
    }

    private static unsafe string GetPixelFormatName(uint format)
    {
        return SdlUtf8.FromNullTerminated(SDL_GetPixelFormatName(format));
    }

    private static unsafe PaletteSnapshot AllocatePalette(int colorCount)
    {
        SDL_Palette* palette = SDL_AllocPalette(colorCount);
        if (palette is null)
        {
            throw new InvalidOperationException($"SDL_AllocPalette failed: {SdlError.Current}");
        }

        try
        {
            return new PaletteSnapshot(
                palette->ncolors,
                palette->colors is not null,
                palette->colors[0].r,
                palette->colors[0].g,
                palette->colors[0].b);
        }
        finally
        {
            SDL_FreePalette(palette);
        }
    }

    private static unsafe AllocationFailureSnapshot TryAllocatePalette(int colorCount)
    {
        SdlError.Clear();

        SDL_Palette* palette = SDL_AllocPalette(colorCount);
        if (palette is not null)
        {
            SDL_FreePalette(palette);
        }

        return new AllocationFailureSnapshot(palette is not null, SdlError.Current);
    }

    private static unsafe GammaRampSnapshot CalculateGammaRamp(float gamma, int sampleIndex = 128)
    {
        const ushort magic = 0xBEEF;

        ushort* ramp = stackalloc ushort[256];
        for (int i = 0; i < 256; i++)
        {
            ramp[i] = magic;
        }

        SDL_CalculateGammaRamp(gamma, ramp);

        int changedCount = 0;
        for (int i = 0; i < 256; i++)
        {
            if (ramp[i] != magic)
            {
                changedCount++;
            }
        }

        return new GammaRampSnapshot(changedCount, ramp[sampleIndex]);
    }

    private static unsafe GammaRampFailureSnapshot CalculateInvalidGammaRamp()
    {
        const ushort magic = 0xBEEF;

        ushort* ramp = stackalloc ushort[256];
        for (int i = 0; i < 256; i++)
        {
            ramp[i] = magic;
        }

        SdlError.Clear();

        SDL_CalculateGammaRamp(-1.0f, ramp);

        int changedCount = 0;
        for (int i = 0; i < 256; i++)
        {
            if (ramp[i] != magic)
            {
                changedCount++;
            }
        }

        return new GammaRampFailureSnapshot(changedCount, SdlError.Current);
    }

    private readonly record struct PixelFormatSnapshot(uint Format, byte BitsPerPixel, byte BytesPerPixel, uint CombinedMasks);

    private readonly record struct PaletteSnapshot(int ColorCount, bool ColorsPointerIsNonNull, byte Red, byte Green, byte Blue);

    private readonly record struct AllocationFailureSnapshot(bool Allocated, string Error);

    private readonly record struct GammaRampSnapshot(int ChangedCount, ushort Sample);

    private readonly record struct GammaRampFailureSnapshot(int ChangedCount, string Error);
}
