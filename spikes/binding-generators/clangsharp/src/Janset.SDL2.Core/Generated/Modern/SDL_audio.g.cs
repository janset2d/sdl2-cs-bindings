using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace SDL2
{
    public unsafe partial struct SDL_AudioSpec
    {
        public int freq;

        [NativeTypeName("SDL_AudioFormat")]
        public ushort format;

        [NativeTypeName("Uint8")]
        public byte channels;

        [NativeTypeName("Uint8")]
        public byte silence;

        [NativeTypeName("Uint16")]
        public ushort samples;

        [NativeTypeName("Uint16")]
        public ushort padding;

        [NativeTypeName("Uint32")]
        public uint size;

        [NativeTypeName("SDL_AudioCallback")]
        public delegate* unmanaged[Cdecl]<nint, byte*, int, void> callback;

        [NativeTypeName("void*")]
        public nint userdata;
    }

    public unsafe partial struct SDL_AudioCVT
    {
        public int needed;

        [NativeTypeName("SDL_AudioFormat")]
        public ushort src_format;

        [NativeTypeName("SDL_AudioFormat")]
        public ushort dst_format;

        public double rate_incr;

        [NativeTypeName("Uint8 *")]
        public byte* buf;

        public int len;

        public int len_cvt;

        public int len_mult;

        public double len_ratio;

        [NativeTypeName("SDL_AudioFilter[10]")]
        public _filters_e__FixedBuffer filters;

        public int filter_index;

        public unsafe partial struct _filters_e__FixedBuffer
        {
            public delegate* unmanaged[Cdecl]<SDL_AudioCVT*, ushort, void> e0;
            public delegate* unmanaged[Cdecl]<SDL_AudioCVT*, ushort, void> e1;
            public delegate* unmanaged[Cdecl]<SDL_AudioCVT*, ushort, void> e2;
            public delegate* unmanaged[Cdecl]<SDL_AudioCVT*, ushort, void> e3;
            public delegate* unmanaged[Cdecl]<SDL_AudioCVT*, ushort, void> e4;
            public delegate* unmanaged[Cdecl]<SDL_AudioCVT*, ushort, void> e5;
            public delegate* unmanaged[Cdecl]<SDL_AudioCVT*, ushort, void> e6;
            public delegate* unmanaged[Cdecl]<SDL_AudioCVT*, ushort, void> e7;
            public delegate* unmanaged[Cdecl]<SDL_AudioCVT*, ushort, void> e8;
            public delegate* unmanaged[Cdecl]<SDL_AudioCVT*, ushort, void> e9;

            public ref delegate* unmanaged[Cdecl]<SDL_AudioCVT*, ushort, void> this[int index]
            {
                get
                {
                    fixed (delegate* unmanaged[Cdecl]<SDL_AudioCVT*, ushort, void>* pThis = &e0)
                    {
                        return ref pThis[index];
                    }
                }
            }
        }
    }

    public enum SDL_AudioStatus
    {
        SDL_AUDIO_STOPPED = 0,
        SDL_AUDIO_PLAYING,
        SDL_AUDIO_PAUSED,
    }

    public partial struct SDL_AudioStream
    {
    }

    internal static unsafe partial class SDLNative
    {
        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_GetNumAudioDrivers();

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("const char *")]
        public static partial byte* SDL_GetAudioDriver(int index);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_AudioInit([NativeTypeName("const char *")] byte* driver_name);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_AudioQuit();

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("const char *")]
        public static partial byte* SDL_GetCurrentAudioDriver();

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_OpenAudio(SDL_AudioSpec* desired, SDL_AudioSpec* obtained);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_GetNumAudioDevices(int iscapture);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("const char *")]
        public static partial byte* SDL_GetAudioDeviceName(int index, int iscapture);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_GetAudioDeviceSpec(int index, int iscapture, SDL_AudioSpec* spec);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_GetDefaultAudioInfo([NativeTypeName("char **")] byte** name, SDL_AudioSpec* spec, int iscapture);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("SDL_AudioDeviceID")]
        public static partial uint SDL_OpenAudioDevice([NativeTypeName("const char *")] byte* device, int iscapture, [NativeTypeName("const SDL_AudioSpec *")] SDL_AudioSpec* desired, SDL_AudioSpec* obtained, int allowed_changes);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_AudioStatus SDL_GetAudioStatus();

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_AudioStatus SDL_GetAudioDeviceStatus([NativeTypeName("SDL_AudioDeviceID")] uint dev);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_PauseAudio(int pause_on);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_PauseAudioDevice([NativeTypeName("SDL_AudioDeviceID")] uint dev, int pause_on);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_AudioSpec* SDL_LoadWAV_RW(SDL_RWops* src, int freesrc, SDL_AudioSpec* spec, [NativeTypeName("Uint8 **")] byte** audio_buf, [NativeTypeName("Uint32 *")] uint* audio_len);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_FreeWAV([NativeTypeName("Uint8 *")] byte* audio_buf);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_BuildAudioCVT(SDL_AudioCVT* cvt, [NativeTypeName("SDL_AudioFormat")] ushort src_format, [NativeTypeName("Uint8")] byte src_channels, int src_rate, [NativeTypeName("SDL_AudioFormat")] ushort dst_format, [NativeTypeName("Uint8")] byte dst_channels, int dst_rate);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_ConvertAudio(SDL_AudioCVT* cvt);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_AudioStream* SDL_NewAudioStream([NativeTypeName("const SDL_AudioFormat")] ushort src_format, [NativeTypeName("const Uint8")] byte src_channels, [NativeTypeName("const int")] int src_rate, [NativeTypeName("const SDL_AudioFormat")] ushort dst_format, [NativeTypeName("const Uint8")] byte dst_channels, [NativeTypeName("const int")] int dst_rate);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_AudioStreamPut(SDL_AudioStream* stream, [NativeTypeName("const void *")] nint buf, int len);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_AudioStreamGet(SDL_AudioStream* stream, [NativeTypeName("void*")] nint buf, int len);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_AudioStreamAvailable(SDL_AudioStream* stream);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_AudioStreamFlush(SDL_AudioStream* stream);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_AudioStreamClear(SDL_AudioStream* stream);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_FreeAudioStream(SDL_AudioStream* stream);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_MixAudio([NativeTypeName("Uint8 *")] byte* dst, [NativeTypeName("const Uint8 *")] byte* src, [NativeTypeName("Uint32")] uint len, int volume);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_MixAudioFormat([NativeTypeName("Uint8 *")] byte* dst, [NativeTypeName("const Uint8 *")] byte* src, [NativeTypeName("SDL_AudioFormat")] ushort format, [NativeTypeName("Uint32")] uint len, int volume);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_QueueAudio([NativeTypeName("SDL_AudioDeviceID")] uint dev, [NativeTypeName("const void *")] nint data, [NativeTypeName("Uint32")] uint len);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("Uint32")]
        public static partial uint SDL_DequeueAudio([NativeTypeName("SDL_AudioDeviceID")] uint dev, [NativeTypeName("void*")] nint data, [NativeTypeName("Uint32")] uint len);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("Uint32")]
        public static partial uint SDL_GetQueuedAudioSize([NativeTypeName("SDL_AudioDeviceID")] uint dev);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_ClearQueuedAudio([NativeTypeName("SDL_AudioDeviceID")] uint dev);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_LockAudio();

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_LockAudioDevice([NativeTypeName("SDL_AudioDeviceID")] uint dev);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_UnlockAudio();

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_UnlockAudioDevice([NativeTypeName("SDL_AudioDeviceID")] uint dev);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_CloseAudio();

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_CloseAudioDevice([NativeTypeName("SDL_AudioDeviceID")] uint dev);

        [NativeTypeName("#define SDL_AUDIO_MASK_BITSIZE (0xFF)")]
        public const int SDL_AUDIO_MASK_BITSIZE = (0xFF);

        [NativeTypeName("#define SDL_AUDIO_MASK_DATATYPE (1<<8)")]
        public const int SDL_AUDIO_MASK_DATATYPE = (1 << 8);

        [NativeTypeName("#define SDL_AUDIO_MASK_ENDIAN (1<<12)")]
        public const int SDL_AUDIO_MASK_ENDIAN = (1 << 12);

        [NativeTypeName("#define SDL_AUDIO_MASK_SIGNED (1<<15)")]
        public const int SDL_AUDIO_MASK_SIGNED = (1 << 15);

        [NativeTypeName("#define AUDIO_U8 0x0008")]
        public const int AUDIO_U8 = 0x0008;

        [NativeTypeName("#define AUDIO_S8 0x8008")]
        public const int AUDIO_S8 = 0x8008;

        [NativeTypeName("#define AUDIO_U16LSB 0x0010")]
        public const int AUDIO_U16LSB = 0x0010;

        [NativeTypeName("#define AUDIO_S16LSB 0x8010")]
        public const int AUDIO_S16LSB = 0x8010;

        [NativeTypeName("#define AUDIO_U16MSB 0x1010")]
        public const int AUDIO_U16MSB = 0x1010;

        [NativeTypeName("#define AUDIO_S16MSB 0x9010")]
        public const int AUDIO_S16MSB = 0x9010;

        [NativeTypeName("#define AUDIO_U16 AUDIO_U16LSB")]
        public const int AUDIO_U16 = 0x0010;

        [NativeTypeName("#define AUDIO_S16 AUDIO_S16LSB")]
        public const int AUDIO_S16 = 0x8010;

        [NativeTypeName("#define AUDIO_S32LSB 0x8020")]
        public const int AUDIO_S32LSB = 0x8020;

        [NativeTypeName("#define AUDIO_S32MSB 0x9020")]
        public const int AUDIO_S32MSB = 0x9020;

        [NativeTypeName("#define AUDIO_S32 AUDIO_S32LSB")]
        public const int AUDIO_S32 = 0x8020;

        [NativeTypeName("#define AUDIO_F32LSB 0x8120")]
        public const int AUDIO_F32LSB = 0x8120;

        [NativeTypeName("#define AUDIO_F32MSB 0x9120")]
        public const int AUDIO_F32MSB = 0x9120;

        [NativeTypeName("#define AUDIO_F32 AUDIO_F32LSB")]
        public const int AUDIO_F32 = 0x8120;

        [NativeTypeName("#define AUDIO_U16SYS AUDIO_U16LSB")]
        public const int AUDIO_U16SYS = 0x0010;

        [NativeTypeName("#define AUDIO_S16SYS AUDIO_S16LSB")]
        public const int AUDIO_S16SYS = 0x8010;

        [NativeTypeName("#define AUDIO_S32SYS AUDIO_S32LSB")]
        public const int AUDIO_S32SYS = 0x8020;

        [NativeTypeName("#define AUDIO_F32SYS AUDIO_F32LSB")]
        public const int AUDIO_F32SYS = 0x8120;

        [NativeTypeName("#define SDL_AUDIO_ALLOW_FREQUENCY_CHANGE 0x00000001")]
        public const int SDL_AUDIO_ALLOW_FREQUENCY_CHANGE = 0x00000001;

        [NativeTypeName("#define SDL_AUDIO_ALLOW_FORMAT_CHANGE 0x00000002")]
        public const int SDL_AUDIO_ALLOW_FORMAT_CHANGE = 0x00000002;

        [NativeTypeName("#define SDL_AUDIO_ALLOW_CHANNELS_CHANGE 0x00000004")]
        public const int SDL_AUDIO_ALLOW_CHANNELS_CHANGE = 0x00000004;

        [NativeTypeName("#define SDL_AUDIO_ALLOW_SAMPLES_CHANGE 0x00000008")]
        public const int SDL_AUDIO_ALLOW_SAMPLES_CHANGE = 0x00000008;

        [NativeTypeName("#define SDL_AUDIO_ALLOW_ANY_CHANGE (SDL_AUDIO_ALLOW_FREQUENCY_CHANGE|SDL_AUDIO_ALLOW_FORMAT_CHANGE|SDL_AUDIO_ALLOW_CHANNELS_CHANGE|SDL_AUDIO_ALLOW_SAMPLES_CHANGE)")]
        public const int SDL_AUDIO_ALLOW_ANY_CHANGE = (0x00000001 | 0x00000002 | 0x00000004 | 0x00000008);

        [NativeTypeName("#define SDL_AUDIOCVT_MAX_FILTERS 9")]
        public const int SDL_AUDIOCVT_MAX_FILTERS = 9;

        [NativeTypeName("#define SDL_MIX_MAXVOLUME 128")]
        public const int SDL_MIX_MAXVOLUME = 128;
    }
}
