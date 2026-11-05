using System;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

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

    internal static unsafe partial class SDL_mixerNative
    {
        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("const SDL_version *")]
        public static partial SDL_version* Mix_Linked_Version();

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int Mix_Init(int flags);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void Mix_Quit();

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int Mix_OpenAudio(int frequency, [NativeTypeName("Uint16")] ushort format, int channels, int chunksize);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int Mix_OpenAudioDevice(int frequency, [NativeTypeName("Uint16")] ushort format, int channels, int chunksize, [NativeTypeName("const char *")] byte* device, int allowed_changes);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void Mix_PauseAudio(int pause_on);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int Mix_QuerySpec(int* frequency, [NativeTypeName("Uint16 *")] ushort* format, int* channels);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int Mix_AllocateChannels(int numchans);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial Mix_Chunk* Mix_LoadWAV_RW(SDL_RWops src, int freesrc);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial Mix_Chunk* Mix_LoadWAV([NativeTypeName("const char *")] byte* file);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial Mix_Music Mix_LoadMUS([NativeTypeName("const char *")] byte* file);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial Mix_Music Mix_LoadMUS_RW(SDL_RWops src, int freesrc);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial Mix_Music Mix_LoadMUSType_RW(SDL_RWops src, Mix_MusicType type, int freesrc);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial Mix_Chunk* Mix_QuickLoad_WAV([NativeTypeName("Uint8 *")] byte* mem);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial Mix_Chunk* Mix_QuickLoad_RAW([NativeTypeName("Uint8 *")] byte* mem, [NativeTypeName("Uint32")] uint len);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void Mix_FreeChunk(Mix_Chunk* chunk);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void Mix_FreeMusic(Mix_Music music);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int Mix_GetNumChunkDecoders();

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("const char *")]
        public static partial byte* Mix_GetChunkDecoder(int index);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_bool Mix_HasChunkDecoder([NativeTypeName("const char *")] byte* name);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int Mix_GetNumMusicDecoders();

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("const char *")]
        public static partial byte* Mix_GetMusicDecoder(int index);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_bool Mix_HasMusicDecoder([NativeTypeName("const char *")] byte* name);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial Mix_MusicType Mix_GetMusicType([NativeTypeName("const Mix_Music *")] Mix_Music music);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("const char *")]
        public static partial byte* Mix_GetMusicTitle([NativeTypeName("const Mix_Music *")] Mix_Music music);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("const char *")]
        public static partial byte* Mix_GetMusicTitleTag([NativeTypeName("const Mix_Music *")] Mix_Music music);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("const char *")]
        public static partial byte* Mix_GetMusicArtistTag([NativeTypeName("const Mix_Music *")] Mix_Music music);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("const char *")]
        public static partial byte* Mix_GetMusicAlbumTag([NativeTypeName("const Mix_Music *")] Mix_Music music);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("const char *")]
        public static partial byte* Mix_GetMusicCopyrightTag([NativeTypeName("const Mix_Music *")] Mix_Music music);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void Mix_SetPostMix([NativeTypeName("Mix_MixCallback")] delegate* unmanaged[Cdecl]<nint, byte*, int, void> mix_func, [NativeTypeName("void*")] nint arg);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void Mix_HookMusic([NativeTypeName("Mix_MixCallback")] delegate* unmanaged[Cdecl]<nint, byte*, int, void> mix_func, [NativeTypeName("void*")] nint arg);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void Mix_HookMusicFinished([NativeTypeName("Mix_MusicFinishedCallback")] delegate* unmanaged[Cdecl]<void> music_finished);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("void*")]
        public static partial nint Mix_GetMusicHookData();

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void Mix_ChannelFinished([NativeTypeName("Mix_ChannelFinishedCallback")] delegate* unmanaged[Cdecl]<int, void> channel_finished);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int Mix_RegisterEffect(int chan, [NativeTypeName("Mix_EffectFunc_t")] delegate* unmanaged[Cdecl]<int, nint, int, nint, void> f, [NativeTypeName("Mix_EffectDone_t")] delegate* unmanaged[Cdecl]<int, nint, void> d, [NativeTypeName("void*")] nint arg);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int Mix_UnregisterEffect(int channel, [NativeTypeName("Mix_EffectFunc_t")] delegate* unmanaged[Cdecl]<int, nint, int, nint, void> f);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int Mix_UnregisterAllEffects(int channel);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int Mix_SetPanning(int channel, [NativeTypeName("Uint8")] byte left, [NativeTypeName("Uint8")] byte right);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int Mix_SetPosition(int channel, [NativeTypeName("Sint16")] short angle, [NativeTypeName("Uint8")] byte distance);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int Mix_SetDistance(int channel, [NativeTypeName("Uint8")] byte distance);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int Mix_SetReverseStereo(int channel, int flip);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int Mix_ReserveChannels(int num);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int Mix_GroupChannel(int which, int tag);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int Mix_GroupChannels(int from, int to, int tag);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int Mix_GroupAvailable(int tag);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int Mix_GroupCount(int tag);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int Mix_GroupOldest(int tag);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int Mix_GroupNewer(int tag);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int Mix_PlayChannel(int channel, Mix_Chunk* chunk, int loops);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int Mix_PlayChannelTimed(int channel, Mix_Chunk* chunk, int loops, int ticks);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int Mix_PlayMusic(Mix_Music music, int loops);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int Mix_FadeInMusic(Mix_Music music, int loops, int ms);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int Mix_FadeInMusicPos(Mix_Music music, int loops, int ms, double position);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int Mix_FadeInChannel(int channel, Mix_Chunk* chunk, int loops, int ms);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int Mix_FadeInChannelTimed(int channel, Mix_Chunk* chunk, int loops, int ms, int ticks);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int Mix_Volume(int channel, int volume);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int Mix_VolumeChunk(Mix_Chunk* chunk, int volume);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int Mix_VolumeMusic(int volume);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int Mix_GetMusicVolume(Mix_Music music);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int Mix_MasterVolume(int volume);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int Mix_HaltChannel(int channel);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int Mix_HaltGroup(int tag);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int Mix_HaltMusic();

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int Mix_ExpireChannel(int channel, int ticks);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int Mix_FadeOutChannel(int which, int ms);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int Mix_FadeOutGroup(int tag, int ms);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int Mix_FadeOutMusic(int ms);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial Mix_Fading Mix_FadingMusic();

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial Mix_Fading Mix_FadingChannel(int which);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void Mix_Pause(int channel);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void Mix_Resume(int channel);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int Mix_Paused(int channel);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void Mix_PauseMusic();

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void Mix_ResumeMusic();

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void Mix_RewindMusic();

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int Mix_PausedMusic();

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int Mix_ModMusicJumpToOrder(int order);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int Mix_StartTrack(Mix_Music music, int track);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int Mix_GetNumTracks(Mix_Music music);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int Mix_SetMusicPosition(double position);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial double Mix_GetMusicPosition(Mix_Music music);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial double Mix_MusicDuration(Mix_Music music);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial double Mix_GetMusicLoopStartTime(Mix_Music music);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial double Mix_GetMusicLoopEndTime(Mix_Music music);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial double Mix_GetMusicLoopLengthTime(Mix_Music music);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int Mix_Playing(int channel);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int Mix_PlayingMusic();

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int Mix_SetMusicCMD([NativeTypeName("const char *")] byte* command);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int Mix_SetSynchroValue(int value);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int Mix_GetSynchroValue();

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int Mix_SetSoundFonts([NativeTypeName("const char *")] byte* paths);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("const char *")]
        public static partial byte* Mix_GetSoundFonts();

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int Mix_EachSoundFont([NativeTypeName("Mix_EachSoundFontCallback")] delegate* unmanaged[Cdecl]<byte*, nint, int> function, [NativeTypeName("void*")] nint data);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int Mix_SetTimidityCfg([NativeTypeName("const char *")] byte* path);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("const char *")]
        public static partial byte* Mix_GetTimidityCfg();

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial Mix_Chunk* Mix_GetChunk(int channel);

        [LibraryImport("SDL2_mixer")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void Mix_CloseAudio();

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
        public static ReadOnlySpan<byte> MIX_EFFECTSMAXSPEED => "MIX_EFFECTSMAXSPEED"u8;
    }
}
