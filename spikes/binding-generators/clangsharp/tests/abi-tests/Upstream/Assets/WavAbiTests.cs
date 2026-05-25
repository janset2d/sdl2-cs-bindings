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
        await Assert.That(result.Format).IsNotEqualTo((ushort)0);
        await Assert.That(result.Channels).IsGreaterThan((byte)0);
        await Assert.That(result.Samples).IsGreaterThan((ushort)0);
        await Assert.That(result.AudioLength).IsGreaterThan(0U);
    }

    [Test]
    [Category(AbiCategories.UpstreamPort)]
    [Category(AbiCategories.Assets)]
    [Category(AbiCategories.SdlAudio)]
    [UpstreamSdlTest("test/testautomation_audio.c", "audio_buildAudioCVT")]
    public async Task SDLBuildAudioCvt_Should_Return_Zero_When_Source_And_Target_Match()
    {
        AudioCvtResult result = BuildAudioCvt(AUDIO_S16LSB, 2, 22050, AUDIO_S16LSB, 2, 22050);

        await Assert.That(result.ReturnCode).IsEqualTo(0);
        await Assert.That(result.Needed).IsEqualTo(0);
        await Assert.That(result.LengthMultiplier).IsGreaterThan(0);
    }

    [Test]
    [Category(AbiCategories.UpstreamPort)]
    [Category(AbiCategories.Assets)]
    [Category(AbiCategories.SdlAudio)]
    [UpstreamSdlTest("test/testautomation_audio.c", "audio_buildAudioCVT")]
    public async Task SDLBuildAudioCvt_Should_Create_Conversion_When_Format_Channels_And_Frequency_Change()
    {
        AudioCvtResult result = BuildAudioCvt(AUDIO_S8, 1, 22050, AUDIO_S16LSB, 2, 44100);

        await Assert.That(result.ReturnCode).IsEqualTo(1);
        await Assert.That(result.Needed).IsEqualTo(1);
        await Assert.That(result.SourceFormat).IsEqualTo((ushort)AUDIO_S8);
        await Assert.That(result.DestinationFormat).IsEqualTo((ushort)AUDIO_S16LSB);
        await Assert.That(result.LengthMultiplier).IsGreaterThan(0);
        await Assert.That(result.LengthRatio).IsGreaterThan(0.0);
    }

    [Test]
    [NotInParallel(AbiParallelKeys.Error)]
    [Category(AbiCategories.UpstreamPort)]
    [Category(AbiCategories.Assets)]
    [Category(AbiCategories.SdlAudio)]
    [Category(AbiCategories.SdlError)]
    [UpstreamSdlTest("test/testautomation_audio.c", "audio_buildAudioCVTNegative")]
    public async Task SDLBuildAudioCvt_Should_Return_Error_When_Cvt_Is_Null()
    {
        AudioErrorResult result = BuildAudioCvtWithNullCvt();

        await Assert.That(result.ReturnCode).IsEqualTo(-1);
        await Assert.That(result.Error).IsEqualTo("Parameter 'cvt' is invalid");
    }

    [Test]
    [Category(AbiCategories.Smoke)]
    [Category(AbiCategories.Assets)]
    [Category(AbiCategories.SdlAudio)]
    public async Task SDLConvertAudio_Should_Convert_Small_Buffer_When_Cvt_Is_Built()
    {
        AudioConversionResult result = ConvertSmallAudioBuffer();

        await Assert.That(result.BuildReturnCode).IsEqualTo(1);
        await Assert.That(result.ConvertReturnCode).IsEqualTo(0);
        await Assert.That(result.ConvertedLength).IsGreaterThan(0);
        await Assert.That(result.ConvertedLength).IsLessThanOrEqualTo(result.BufferLength);
        await Assert.That(result.LengthRatio).IsGreaterThan(0.0);
    }

    [Test]
    [Category(AbiCategories.Smoke)]
    [Category(AbiCategories.Assets)]
    [Category(AbiCategories.SdlAudio)]
    public async Task SDLAudioStream_Should_Round_Trip_And_Clear_Buffer_When_Format_Does_Not_Change()
    {
        AudioStreamResult result = RoundTripAudioStream();

        await Assert.That(result.Created).IsTrue();
        await Assert.That(result.PutReturnCode).IsEqualTo(0);
        await Assert.That(result.FlushReturnCode).IsEqualTo(0);
        await Assert.That(result.AvailableAfterPut).IsEqualTo(result.InputLength);
        await Assert.That(result.ReadLength).IsEqualTo(result.InputLength);
        await Assert.That(result.AvailableAfterGet).IsEqualTo(0);
        await Assert.That(result.AvailableAfterClear).IsEqualTo(0);

        for (int i = 0; i < result.Output.Length; i++)
        {
            await Assert.That(result.Output[i]).IsEqualTo(result.Input[i]);
        }
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

            return new WavResult(spec.freq, spec.format, spec.channels, spec.samples, audioLength);
        }
        finally
        {
            SDL_FreeWAV(audioBuffer);
        }
    }

    private static unsafe AudioCvtResult BuildAudioCvt(
        int sourceFormat,
        byte sourceChannels,
        int sourceRate,
        int destinationFormat,
        byte destinationChannels,
        int destinationRate)
    {
        SDL_AudioCVT cvt = default;
        int result = SDL_BuildAudioCVT(
            &cvt,
            (ushort)sourceFormat,
            sourceChannels,
            sourceRate,
            (ushort)destinationFormat,
            destinationChannels,
            destinationRate);

        return new AudioCvtResult(result, cvt.needed, cvt.src_format, cvt.dst_format, cvt.len_mult, cvt.len_ratio);
    }

    private static unsafe AudioErrorResult BuildAudioCvtWithNullCvt()
    {
        SdlError.Clear();
        int result = SDL_BuildAudioCVT(null, AUDIO_S8, 1, 22050, AUDIO_S16LSB, 2, 44100);
        string error = SdlError.Current;
        SdlError.Clear();

        return new AudioErrorResult(result, error);
    }

    private static unsafe AudioConversionResult ConvertSmallAudioBuffer()
    {
        SDL_AudioCVT cvt = default;
        int buildResult = SDL_BuildAudioCVT(&cvt, AUDIO_S8, 1, 22050, AUDIO_S16LSB, 2, 44100);
        if (buildResult != 1)
        {
            return new AudioConversionResult(buildResult, -1, cvt.len_cvt, 0, cvt.len_ratio);
        }

        byte[] buffer = new byte[64 * cvt.len_mult];
        for (int i = 0; i < 64; i++)
        {
            buffer[i] = (byte)i;
        }

        fixed (byte* bufferPointer = buffer)
        {
            cvt.len = 64;
            cvt.buf = bufferPointer;
            int convertResult = SDL_ConvertAudio(&cvt);

            return new AudioConversionResult(buildResult, convertResult, cvt.len_cvt, buffer.Length, cvt.len_ratio);
        }
    }

    private static unsafe AudioStreamResult RoundTripAudioStream()
    {
        byte[] input = [1, 3, 5, 7, 11, 13, 17, 19];
        byte[] output = new byte[input.Length];
        SDL_AudioStream stream = SDL_NewAudioStream(AUDIO_U8, 1, 22050, AUDIO_U8, 1, 22050);

        if (stream.IsNull)
        {
            return new AudioStreamResult(false, -1, -1, -1, -1, -1, -1, input, output);
        }

        try
        {
            fixed (byte* inputPointer = input)
            fixed (byte* outputPointer = output)
            {
                int putResult = SDL_AudioStreamPut(stream, (nint)inputPointer, input.Length);
                int flushResult = SDL_AudioStreamFlush(stream);
                int availableAfterPut = SDL_AudioStreamAvailable(stream);
                int readLength = SDL_AudioStreamGet(stream, (nint)outputPointer, output.Length);
                int availableAfterGet = SDL_AudioStreamAvailable(stream);

                SDL_AudioStreamClear(stream);
                int availableAfterClear = SDL_AudioStreamAvailable(stream);

                return new AudioStreamResult(
                    true,
                    putResult,
                    flushResult,
                    availableAfterPut,
                    readLength,
                    availableAfterGet,
                    availableAfterClear,
                    input,
                    output);
            }
        }
        finally
        {
            SDL_FreeAudioStream(stream);
        }
    }

    private sealed record WavResult(int Frequency, ushort Format, byte Channels, ushort Samples, uint AudioLength);

    private sealed record AudioCvtResult(int ReturnCode, int Needed, ushort SourceFormat, ushort DestinationFormat, int LengthMultiplier, double LengthRatio);

    private sealed record AudioErrorResult(int ReturnCode, string Error);

    private sealed record AudioConversionResult(int BuildReturnCode, int ConvertReturnCode, int ConvertedLength, int BufferLength, double LengthRatio);

    private sealed record AudioStreamResult(
        bool Created,
        int PutReturnCode,
        int FlushReturnCode,
        int AvailableAfterPut,
        int ReadLength,
        int AvailableAfterGet,
        int AvailableAfterClear,
        byte[] Input,
        byte[] Output)
    {
        public int InputLength => Input.Length;
    }
}
