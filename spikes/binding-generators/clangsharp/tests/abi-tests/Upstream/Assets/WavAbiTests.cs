using Janset.SDL2.AbiTests.Infrastructure.Assets;
using Janset.SDL2.AbiTests.Infrastructure.Classification;
using Janset.SDL2.AbiTests.Infrastructure.Sdl;
using SDL2;
using static SDL2.SDLNative;

namespace Janset.SDL2.AbiTests.Upstream.Assets;

public sealed class WavAbiTests
{
    [Test]
    [Category(AbiCategories.UpstreamPort)]
    [Category(AbiCategories.Assets)]
    [Category(AbiCategories.SdlAudio)]
    [UpstreamSdlTest("test/loopwave.c", "SDL_LoadWAV")]
    public async Task SDLLoadWavRw_Should_Load_Upstream_Sample_Wav()
    {
        string samplePath = UpstreamSdlAsset.Path("sample.wav");

        WavResult result = LoadWav(samplePath);

        await Assert.That(result.Frequency).IsGreaterThan(0);
        await Assert.That(result.Channels).IsGreaterThan((byte)0);
        await Assert.That(result.Samples).IsGreaterThan((ushort)0);
        await Assert.That(result.AudioLength).IsGreaterThan(0U);
    }

    private static unsafe WavResult LoadWav(string samplePath)
    {
        byte* audioBuffer = null;

        try
        {
            using PinnedUtf8 pinnedSamplePath = SdlUtf8.Pin(samplePath);
            using PinnedUtf8 readMode = SdlUtf8.Pin("rb");

            SDL_RWops source = SDL_RWFromFile(pinnedSamplePath.Pointer, readMode.Pointer);
            if (source.IsNull)
            {
                throw new InvalidOperationException($"SDL_RWFromFile failed for sample WAV: {SdlError.Current}");
            }

            SDL_AudioSpec spec = default;
            uint audioLength = 0;
            SDL_AudioSpec* loadedSpec = SDL_LoadWAV_RW(source, 1, &spec, &audioBuffer, &audioLength);
            if (loadedSpec is null)
            {
                throw new InvalidOperationException($"SDL_LoadWAV_RW failed: {SdlError.Current}");
            }

            return new WavResult(spec.freq, spec.channels, spec.samples, audioLength);
        }
        finally
        {
            SDL_FreeWAV(audioBuffer);
        }
    }

    private sealed record WavResult(int Frequency, byte Channels, ushort Samples, uint AudioLength);
}
