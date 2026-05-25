using Janset.SDL2.AbiTests.Infrastructure.Classification;
using Janset.SDL2.AbiTests.Infrastructure.Sdl;
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
    [UpstreamSdlTest("test/testautomation_audio.c", "audio_initQuitAudio")]
    public async Task SDLAudioInit_Should_Init_And_Quit_Dummy_Driver()
    {
        int result = InitAndQuitDummyAudioDriver();

        await Assert.That(result).IsEqualTo(0);
    }

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

    [Test]
    [NotInParallel(AbiParallelKeys.Audio)]
    [RequiresAudioDummyDriver]
    [Category(AbiCategories.UpstreamPort)]
    [Category(AbiCategories.DummyDriver)]
    [Category(AbiCategories.SdlAudio)]
    [UpstreamSdlTest("test/testautomation_audio.c", "audio_getAudioStatus")]
    public async Task SDLGetAudioStatus_Should_Return_Known_Status()
    {
        SDL_AudioStatus status = GetDummyAudioStatus();

        await Assert.That(IsKnownAudioStatus(status)).IsTrue();
    }

    [Test]
    [NotInParallel(AbiParallelKeys.Audio)]
    [RequiresAudioDummyDriver]
    [Category(AbiCategories.UpstreamPort)]
    [Category(AbiCategories.DummyDriver)]
    [Category(AbiCategories.SdlAudio)]
    [UpstreamSdlTest("test/testautomation_audio.c", "audio_openCloseAndGetAudioStatus")]
    public async Task SDLGetAudioDeviceStatus_Should_Return_Known_Status_For_Dummy_Device()
    {
        AudioDeviceStatusResult result = OpenDummyAudioDeviceAndGetStatus();

        await Assert.That(result.DeviceCount).IsGreaterThan(0);
        await Assert.That(result.DeviceId).IsNotEqualTo(0U);
        await Assert.That(IsKnownAudioStatus(result.Status)).IsTrue();
    }

    [Test]
    [NotInParallel(AbiParallelKeys.Audio)]
    [RequiresAudioDummyDriver]
    [Category(AbiCategories.UpstreamPort)]
    [Category(AbiCategories.DummyDriver)]
    [Category(AbiCategories.SdlAudio)]
    [UpstreamSdlTest("test/testautomation_audio.c", "audio_lockUnlockOpenAudioDevice")]
    public async Task SDLLockAudioDevice_Should_Lock_And_Unlock_Dummy_Device()
    {
        AudioDeviceLockResult result = LockAndUnlockDummyAudioDevice();

        await Assert.That(result.DeviceCount).IsGreaterThan(0);
        await Assert.That(result.DeviceId).IsNotEqualTo(0U);
    }

    private static unsafe int InitAndQuitDummyAudioDriver()
    {
        using SdlEnvironmentScope audioDriver = new("SDL_AUDIODRIVER", "dummy");
        using PinnedUtf8 driverName = SdlUtf8.Pin("dummy");

        SDL_QuitSubSystem(SDL_INIT_AUDIO);
        int result = SDL_AudioInit(driverName.Pointer);
        SDL_AudioQuit();

        return result;
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

    private static SDL_AudioStatus GetDummyAudioStatus()
    {
        using SdlEnvironmentScope audioDriver = new("SDL_AUDIODRIVER", "dummy");
        using SdlSubsystemScope audio = new(SDL_INIT_AUDIO);

        return SDL_GetAudioStatus();
    }

    private static unsafe AudioDeviceStatusResult OpenDummyAudioDeviceAndGetStatus()
    {
        using SdlEnvironmentScope audioDriver = new("SDL_AUDIODRIVER", "dummy");
        using SdlSubsystemScope audio = new(SDL_INIT_AUDIO);

        int count = SDL_GetNumAudioDevices(0);
        if (count <= 0)
        {
            return new AudioDeviceStatusResult(count, 0, SDL_AudioStatus.SDL_AUDIO_STOPPED);
        }

        byte* deviceName = SDL_GetAudioDeviceName(0, 0);
        SDL_AudioSpec desired = CreateStandardDesiredSpec();
        SDL_AudioSpec obtained = default;
        uint deviceId = SDL_OpenAudioDevice(deviceName, 0, &desired, &obtained, SDL_AUDIO_ALLOW_ANY_CHANGE);
        SDL_AudioStatus status = deviceId == 0 ? SDL_AudioStatus.SDL_AUDIO_STOPPED : SDL_GetAudioDeviceStatus(deviceId);
        if (deviceId != 0)
        {
            SDL_CloseAudioDevice(deviceId);
        }

        return new AudioDeviceStatusResult(count, deviceId, status);
    }

    private static unsafe AudioDeviceLockResult LockAndUnlockDummyAudioDevice()
    {
        using SdlEnvironmentScope audioDriver = new("SDL_AUDIODRIVER", "dummy");
        using SdlSubsystemScope audio = new(SDL_INIT_AUDIO);

        int count = SDL_GetNumAudioDevices(0);
        if (count <= 0)
        {
            return new AudioDeviceLockResult(count, 0);
        }

        byte* deviceName = SDL_GetAudioDeviceName(0, 0);
        SDL_AudioSpec desired = CreateStandardDesiredSpec();
        SDL_AudioSpec obtained = default;
        uint deviceId = SDL_OpenAudioDevice(deviceName, 0, &desired, &obtained, SDL_AUDIO_ALLOW_ANY_CHANGE);
        if (deviceId != 0)
        {
            SDL_LockAudioDevice(deviceId);
            SDL_UnlockAudioDevice(deviceId);
            SDL_CloseAudioDevice(deviceId);
        }

        return new AudioDeviceLockResult(count, deviceId);
    }

    private static SDL_AudioSpec CreateStandardDesiredSpec()
    {
        return new SDL_AudioSpec
        {
            freq = 22050,
            format = AUDIO_S16SYS,
            channels = 2,
            samples = 4096,
        };
    }

    private static bool IsKnownAudioStatus(SDL_AudioStatus status)
    {
        return status is SDL_AudioStatus.SDL_AUDIO_STOPPED
            or SDL_AudioStatus.SDL_AUDIO_PLAYING
            or SDL_AudioStatus.SDL_AUDIO_PAUSED;
    }

    private sealed record AudioDeviceResult(uint DeviceId, int Frequency, byte Channels);

    private sealed record AudioDeviceStatusResult(int DeviceCount, uint DeviceId, SDL_AudioStatus Status);

    private sealed record AudioDeviceLockResult(int DeviceCount, uint DeviceId);
}
