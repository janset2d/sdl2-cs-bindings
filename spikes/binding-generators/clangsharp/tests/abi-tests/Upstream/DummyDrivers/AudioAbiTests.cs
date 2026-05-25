using Janset.SDL2.AbiTests.Infrastructure.Classification;
using Janset.SDL2.AbiTests.Infrastructure.Sdl.Requirements;
using Janset.SDL2.AbiTests.Infrastructure.Sdl.Scopes;
using SDL2;
using static SDL2.SDLNative;

namespace Janset.SDL2.AbiTests.Upstream.DummyDrivers;

public sealed class AudioAbiTests
{
    [Test]
    [NotInParallel(AbiParallelKeys.Audio)]
    [RequiresAudioDummyDriver]
    [Category(AbiCategories.UpstreamPort)]
    [Category(AbiCategories.DummyDriver)]
    [Category(AbiCategories.SdlAudio)]
    [UpstreamSdlTest("test/testautomation_audio.c", "audio_openCloseAudioDevice")]
    public async Task SDLOpenAudioDevice_Should_Open_And_Close_Dummy_Device()
    {
        AudioDeviceResult result = OpenAndCloseDummyAudioDevice();

        await Assert.That(result.DeviceId).IsNotEqualTo(0U);
        await Assert.That(result.Frequency).IsGreaterThan(0);
        await Assert.That(result.Channels).IsGreaterThan((byte)0);
    }

    private static unsafe AudioDeviceResult OpenAndCloseDummyAudioDevice()
    {
        using SdlEnvironmentScope audioDriver = new("SDL_AUDIODRIVER", "dummy");
        using SdlSubsystemScope audio = new(SDL_INIT_AUDIO);

        SDL_AudioSpec desired = new()
        {
            freq = 22050,
            format = AUDIO_U8,
            channels = 1,
            samples = 256,
        };

        SDL_AudioSpec obtained = default;
        uint deviceId = SDL_OpenAudioDevice(null, 0, &desired, &obtained, 0);
        if (deviceId != 0)
        {
            SDL_CloseAudioDevice(deviceId);
        }

        return new AudioDeviceResult(deviceId, obtained.freq, obtained.channels);
    }

    private sealed record AudioDeviceResult(uint DeviceId, int Frequency, byte Channels);
}
