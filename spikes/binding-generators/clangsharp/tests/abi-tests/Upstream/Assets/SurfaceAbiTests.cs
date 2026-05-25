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
    }

    private static unsafe SurfaceResult LoadAndSaveBmp(string samplePath, string savedPath)
    {
        SDL_Surface* surface = null;

        try
        {
            surface = SdlMacro.LoadBmp(samplePath);
            int saveResult = SdlMacro.SaveBmp(surface, savedPath);

            return new SurfaceResult(surface->w, surface->h, surface->pitch, saveResult);
        }
        finally
        {
            if (surface is not null)
            {
                SDL_FreeSurface(surface);
            }
        }
    }

    private sealed record SurfaceResult(int Width, int Height, int Pitch, int SaveResult);
}
