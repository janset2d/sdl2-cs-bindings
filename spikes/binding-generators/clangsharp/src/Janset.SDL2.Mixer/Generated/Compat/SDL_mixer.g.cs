using System;
using System.Runtime.InteropServices;

namespace SDL2.Mixer
{
    [System.Flags]
    public enum MIX_InitFlags
    {
        MIX_INIT_FLAC = 0x00000001,
        MIX_INIT_MOD = 0x00000002,
        MIX_INIT_MP3 = 0x00000008,
        MIX_INIT_OGG = 0x00000010,
        MIX_INIT_MID = 0x00000020,
        MIX_INIT_OPUS = 0x00000040,
        MIX_INIT_WAVPACK = 0x00000080,
    }

    public unsafe partial struct Mix_Chunk
    {
        public int allocated;

        [NativeTypeName("Uint8 *")]
        public byte* abuf;

        [NativeTypeName("Uint32")]
        public uint alen;

        [NativeTypeName("Uint8")]
        public byte volume;
    }

    public enum Mix_Fading
    {
        MIX_NO_FADING,
        MIX_FADING_OUT,
        MIX_FADING_IN,
    }

    public enum Mix_MusicType
    {
        MUS_NONE,
        MUS_CMD,
        MUS_WAV,
        MUS_MOD,
        MUS_MID,
        MUS_OGG,
        MUS_MP3,
        MUS_MP3_MAD_UNUSED,
        MUS_FLAC,
        MUS_MODPLUG_UNUSED,
        MUS_OPUS,
        MUS_WAVPACK,
        MUS_GME,
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public unsafe delegate void Mix_MixCallback([NativeTypeName("void*")] nint udata, [NativeTypeName("Uint8 *")] byte* stream, int len);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void Mix_MusicFinishedCallback();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void Mix_ChannelFinishedCallback(int channel);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void Mix_EffectFunc_t(int chan, [NativeTypeName("void*")] nint stream, int len, [NativeTypeName("void*")] nint udata);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void Mix_EffectDone_t(int chan, [NativeTypeName("void*")] nint udata);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public unsafe delegate int Mix_EachSoundFontCallback([NativeTypeName("const char *")] byte* param0, [NativeTypeName("void*")] nint param1);

    internal static unsafe partial class SDL_mixerNative
    {
        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const SDL_version *")]
        public static extern SDL_version* Mix_Linked_Version();

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int Mix_Init(int flags);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void Mix_Quit();

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int Mix_OpenAudio(int frequency, [NativeTypeName("Uint16")] ushort format, int channels, int chunksize);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int Mix_OpenAudioDevice(int frequency, [NativeTypeName("Uint16")] ushort format, int channels, int chunksize, [NativeTypeName("const char *")] byte* device, int allowed_changes);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void Mix_PauseAudio(int pause_on);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int Mix_QuerySpec(int* frequency, [NativeTypeName("Uint16 *")] ushort* format, int* channels);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int Mix_AllocateChannels(int numchans);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern Mix_Chunk* Mix_LoadWAV_RW(SDL_RWops src, int freesrc);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern Mix_Chunk* Mix_LoadWAV([NativeTypeName("const char *")] byte* file);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern Mix_Music Mix_LoadMUS([NativeTypeName("const char *")] byte* file);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern Mix_Music Mix_LoadMUS_RW(SDL_RWops src, int freesrc);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern Mix_Music Mix_LoadMUSType_RW(SDL_RWops src, Mix_MusicType type, int freesrc);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern Mix_Chunk* Mix_QuickLoad_WAV([NativeTypeName("Uint8 *")] byte* mem);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern Mix_Chunk* Mix_QuickLoad_RAW([NativeTypeName("Uint8 *")] byte* mem, [NativeTypeName("Uint32")] uint len);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void Mix_FreeChunk(Mix_Chunk* chunk);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void Mix_FreeMusic(Mix_Music music);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int Mix_GetNumChunkDecoders();

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* Mix_GetChunkDecoder(int index);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_bool Mix_HasChunkDecoder([NativeTypeName("const char *")] byte* name);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int Mix_GetNumMusicDecoders();

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* Mix_GetMusicDecoder(int index);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_bool Mix_HasMusicDecoder([NativeTypeName("const char *")] byte* name);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern Mix_MusicType Mix_GetMusicType([NativeTypeName("const Mix_Music *")] Mix_Music music);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* Mix_GetMusicTitle([NativeTypeName("const Mix_Music *")] Mix_Music music);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* Mix_GetMusicTitleTag([NativeTypeName("const Mix_Music *")] Mix_Music music);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* Mix_GetMusicArtistTag([NativeTypeName("const Mix_Music *")] Mix_Music music);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* Mix_GetMusicAlbumTag([NativeTypeName("const Mix_Music *")] Mix_Music music);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* Mix_GetMusicCopyrightTag([NativeTypeName("const Mix_Music *")] Mix_Music music);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void Mix_SetPostMix([NativeTypeName("Mix_MixCallback")] IntPtr mix_func, [NativeTypeName("void*")] nint arg);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void Mix_HookMusic([NativeTypeName("Mix_MixCallback")] IntPtr mix_func, [NativeTypeName("void*")] nint arg);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void Mix_HookMusicFinished([NativeTypeName("Mix_MusicFinishedCallback")] IntPtr music_finished);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("void*")]
        public static extern nint Mix_GetMusicHookData();

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void Mix_ChannelFinished([NativeTypeName("Mix_ChannelFinishedCallback")] IntPtr channel_finished);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int Mix_RegisterEffect(int chan, [NativeTypeName("Mix_EffectFunc_t")] IntPtr f, [NativeTypeName("Mix_EffectDone_t")] IntPtr d, [NativeTypeName("void*")] nint arg);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int Mix_UnregisterEffect(int channel, [NativeTypeName("Mix_EffectFunc_t")] IntPtr f);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int Mix_UnregisterAllEffects(int channel);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int Mix_SetPanning(int channel, [NativeTypeName("Uint8")] byte left, [NativeTypeName("Uint8")] byte right);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int Mix_SetPosition(int channel, [NativeTypeName("Sint16")] short angle, [NativeTypeName("Uint8")] byte distance);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int Mix_SetDistance(int channel, [NativeTypeName("Uint8")] byte distance);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int Mix_SetReverseStereo(int channel, int flip);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int Mix_ReserveChannels(int num);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int Mix_GroupChannel(int which, int tag);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int Mix_GroupChannels(int from, int to, int tag);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int Mix_GroupAvailable(int tag);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int Mix_GroupCount(int tag);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int Mix_GroupOldest(int tag);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int Mix_GroupNewer(int tag);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int Mix_PlayChannel(int channel, Mix_Chunk* chunk, int loops);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int Mix_PlayChannelTimed(int channel, Mix_Chunk* chunk, int loops, int ticks);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int Mix_PlayMusic(Mix_Music music, int loops);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int Mix_FadeInMusic(Mix_Music music, int loops, int ms);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int Mix_FadeInMusicPos(Mix_Music music, int loops, int ms, double position);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int Mix_FadeInChannel(int channel, Mix_Chunk* chunk, int loops, int ms);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int Mix_FadeInChannelTimed(int channel, Mix_Chunk* chunk, int loops, int ms, int ticks);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int Mix_Volume(int channel, int volume);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int Mix_VolumeChunk(Mix_Chunk* chunk, int volume);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int Mix_VolumeMusic(int volume);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int Mix_GetMusicVolume(Mix_Music music);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int Mix_MasterVolume(int volume);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int Mix_HaltChannel(int channel);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int Mix_HaltGroup(int tag);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int Mix_HaltMusic();

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int Mix_ExpireChannel(int channel, int ticks);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int Mix_FadeOutChannel(int which, int ms);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int Mix_FadeOutGroup(int tag, int ms);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int Mix_FadeOutMusic(int ms);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern Mix_Fading Mix_FadingMusic();

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern Mix_Fading Mix_FadingChannel(int which);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void Mix_Pause(int channel);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void Mix_Resume(int channel);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int Mix_Paused(int channel);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void Mix_PauseMusic();

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void Mix_ResumeMusic();

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void Mix_RewindMusic();

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int Mix_PausedMusic();

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int Mix_ModMusicJumpToOrder(int order);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int Mix_StartTrack(Mix_Music music, int track);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int Mix_GetNumTracks(Mix_Music music);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int Mix_SetMusicPosition(double position);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern double Mix_GetMusicPosition(Mix_Music music);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern double Mix_MusicDuration(Mix_Music music);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern double Mix_GetMusicLoopStartTime(Mix_Music music);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern double Mix_GetMusicLoopEndTime(Mix_Music music);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern double Mix_GetMusicLoopLengthTime(Mix_Music music);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int Mix_Playing(int channel);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int Mix_PlayingMusic();

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int Mix_SetMusicCMD([NativeTypeName("const char *")] byte* command);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int Mix_SetSynchroValue(int value);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int Mix_GetSynchroValue();

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int Mix_SetSoundFonts([NativeTypeName("const char *")] byte* paths);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* Mix_GetSoundFonts();

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int Mix_EachSoundFont([NativeTypeName("Mix_EachSoundFontCallback")] IntPtr function, [NativeTypeName("void*")] nint data);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int Mix_SetTimidityCfg([NativeTypeName("const char *")] byte* path);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* Mix_GetTimidityCfg();

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern Mix_Chunk* Mix_GetChunk(int channel);

        [DllImport("SDL2_mixer", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void Mix_CloseAudio();

        [NativeTypeName("#define SDL_MIXER_MAJOR_VERSION 2")]
        public const int SDL_MIXER_MAJOR_VERSION = 2;

        [NativeTypeName("#define SDL_MIXER_MINOR_VERSION 8")]
        public const int SDL_MIXER_MINOR_VERSION = 8;

        [NativeTypeName("#define SDL_MIXER_PATCHLEVEL 1")]
        public const int SDL_MIXER_PATCHLEVEL = 1;

        [NativeTypeName("#define MIX_MAJOR_VERSION SDL_MIXER_MAJOR_VERSION")]
        public const int MIX_MAJOR_VERSION = 2;

        [NativeTypeName("#define MIX_MINOR_VERSION SDL_MIXER_MINOR_VERSION")]
        public const int MIX_MINOR_VERSION = 8;

        [NativeTypeName("#define MIX_PATCHLEVEL SDL_MIXER_PATCHLEVEL")]
        public const int MIX_PATCHLEVEL = 1;

        [NativeTypeName("#define SDL_MIXER_COMPILEDVERSION SDL_VERSIONNUM(SDL_MIXER_MAJOR_VERSION, SDL_MIXER_MINOR_VERSION, SDL_MIXER_PATCHLEVEL)")]
        public const int SDL_MIXER_COMPILEDVERSION = ((2) * 1000 + (8) * 100 + (1));

        [NativeTypeName("#define MIX_CHANNELS 8")]
        public const int MIX_CHANNELS = 8;

        [NativeTypeName("#define MIX_DEFAULT_FREQUENCY 44100")]
        public const int MIX_DEFAULT_FREQUENCY = 44100;

        [NativeTypeName("#define MIX_DEFAULT_FORMAT AUDIO_S16SYS")]
        public const int MIX_DEFAULT_FORMAT = 0x8010;

        [NativeTypeName("#define MIX_DEFAULT_CHANNELS 2")]
        public const int MIX_DEFAULT_CHANNELS = 2;

        [NativeTypeName("#define MIX_MAX_VOLUME SDL_MIX_MAXVOLUME")]
        public const int MIX_MAX_VOLUME = 128;

        [NativeTypeName("#define MIX_CHANNEL_POST (-2)")]
        public const int MIX_CHANNEL_POST = (-2);

        [NativeTypeName("#define MIX_EFFECTSMAXSPEED \"MIX_EFFECTSMAXSPEED\"")]
        public static ReadOnlySpan<byte> MIX_EFFECTSMAXSPEED => new byte[] { 0x4D, 0x49, 0x58, 0x5F, 0x45, 0x46, 0x46, 0x45, 0x43, 0x54, 0x53, 0x4D, 0x41, 0x58, 0x53, 0x50, 0x45, 0x45, 0x44, 0x00 };
    }
}
