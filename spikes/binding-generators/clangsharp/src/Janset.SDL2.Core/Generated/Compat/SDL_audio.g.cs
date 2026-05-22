using System;
using System.Runtime.InteropServices;

namespace SDL2
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public unsafe delegate void SDL_AudioCallback([NativeTypeName("void*")] nint userdata, [NativeTypeName("Uint8 *")] byte* stream, int len);

    public partial struct SDL_AudioSpec
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
        public IntPtr callback;

        [NativeTypeName("void*")]
        public nint userdata;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public unsafe delegate void SDL_AudioFilter([NativeTypeName("struct SDL_AudioCVT *")] SDL_AudioCVT* cvt, [NativeTypeName("SDL_AudioFormat")] ushort format);

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

        public partial struct _filters_e__FixedBuffer
        {
            public IntPtr e0;
            public IntPtr e1;
            public IntPtr e2;
            public IntPtr e3;
            public IntPtr e4;
            public IntPtr e5;
            public IntPtr e6;
            public IntPtr e7;
            public IntPtr e8;
            public IntPtr e9;

            public unsafe ref IntPtr this[int index]
            {
                get
                {
                    fixed (IntPtr* pThis = &e0)
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

    public partial struct _SDL_AudioStream
    {
    }

    public static unsafe partial class SDLNative
    {
        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_GetNumAudioDrivers();

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* SDL_GetAudioDriver(int index);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_AudioInit([NativeTypeName("const char *")] byte* driver_name);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_AudioQuit();

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* SDL_GetCurrentAudioDriver();

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_OpenAudio(SDL_AudioSpec* desired, SDL_AudioSpec* obtained);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_GetNumAudioDevices(int iscapture);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* SDL_GetAudioDeviceName(int index, int iscapture);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_GetAudioDeviceSpec(int index, int iscapture, SDL_AudioSpec* spec);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_GetDefaultAudioInfo([NativeTypeName("char **")] byte** name, SDL_AudioSpec* spec, int iscapture);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("SDL_AudioDeviceID")]
        public static extern uint SDL_OpenAudioDevice([NativeTypeName("const char *")] byte* device, int iscapture, [NativeTypeName("const SDL_AudioSpec *")] SDL_AudioSpec* desired, SDL_AudioSpec* obtained, int allowed_changes);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_AudioStatus SDL_GetAudioStatus();

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_AudioStatus SDL_GetAudioDeviceStatus([NativeTypeName("SDL_AudioDeviceID")] uint dev);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_PauseAudio(int pause_on);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_PauseAudioDevice([NativeTypeName("SDL_AudioDeviceID")] uint dev, int pause_on);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_AudioSpec* SDL_LoadWAV_RW(SDL_RWops* src, int freesrc, SDL_AudioSpec* spec, [NativeTypeName("Uint8 **")] byte** audio_buf, [NativeTypeName("Uint32 *")] uint* audio_len);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_FreeWAV([NativeTypeName("Uint8 *")] byte* audio_buf);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_BuildAudioCVT(SDL_AudioCVT* cvt, [NativeTypeName("SDL_AudioFormat")] ushort src_format, [NativeTypeName("Uint8")] byte src_channels, int src_rate, [NativeTypeName("SDL_AudioFormat")] ushort dst_format, [NativeTypeName("Uint8")] byte dst_channels, int dst_rate);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_ConvertAudio(SDL_AudioCVT* cvt);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("SDL_AudioStream *")]
        public static extern _SDL_AudioStream* SDL_NewAudioStream([NativeTypeName("const SDL_AudioFormat")] ushort src_format, [NativeTypeName("const Uint8")] byte src_channels, [NativeTypeName("const int")] int src_rate, [NativeTypeName("const SDL_AudioFormat")] ushort dst_format, [NativeTypeName("const Uint8")] byte dst_channels, [NativeTypeName("const int")] int dst_rate);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_AudioStreamPut([NativeTypeName("SDL_AudioStream *")] _SDL_AudioStream* stream, [NativeTypeName("const void *")] nint buf, int len);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_AudioStreamGet([NativeTypeName("SDL_AudioStream *")] _SDL_AudioStream* stream, [NativeTypeName("void*")] nint buf, int len);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_AudioStreamAvailable([NativeTypeName("SDL_AudioStream *")] _SDL_AudioStream* stream);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_AudioStreamFlush([NativeTypeName("SDL_AudioStream *")] _SDL_AudioStream* stream);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_AudioStreamClear([NativeTypeName("SDL_AudioStream *")] _SDL_AudioStream* stream);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_FreeAudioStream([NativeTypeName("SDL_AudioStream *")] _SDL_AudioStream* stream);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_MixAudio([NativeTypeName("Uint8 *")] byte* dst, [NativeTypeName("const Uint8 *")] byte* src, [NativeTypeName("Uint32")] uint len, int volume);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_MixAudioFormat([NativeTypeName("Uint8 *")] byte* dst, [NativeTypeName("const Uint8 *")] byte* src, [NativeTypeName("SDL_AudioFormat")] ushort format, [NativeTypeName("Uint32")] uint len, int volume);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_QueueAudio([NativeTypeName("SDL_AudioDeviceID")] uint dev, [NativeTypeName("const void *")] nint data, [NativeTypeName("Uint32")] uint len);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("Uint32")]
        public static extern uint SDL_DequeueAudio([NativeTypeName("SDL_AudioDeviceID")] uint dev, [NativeTypeName("void*")] nint data, [NativeTypeName("Uint32")] uint len);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("Uint32")]
        public static extern uint SDL_GetQueuedAudioSize([NativeTypeName("SDL_AudioDeviceID")] uint dev);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_ClearQueuedAudio([NativeTypeName("SDL_AudioDeviceID")] uint dev);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_LockAudio();

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_LockAudioDevice([NativeTypeName("SDL_AudioDeviceID")] uint dev);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_UnlockAudio();

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_UnlockAudioDevice([NativeTypeName("SDL_AudioDeviceID")] uint dev);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_CloseAudio();

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_CloseAudioDevice([NativeTypeName("SDL_AudioDeviceID")] uint dev);

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
