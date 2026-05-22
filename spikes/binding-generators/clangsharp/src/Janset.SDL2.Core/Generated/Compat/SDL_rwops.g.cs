using System;
using System.Runtime.InteropServices;

namespace SDL2
{
    public partial struct SDL_RWops
    {
        [NativeTypeName("Sint64 (*)(struct SDL_RWops *) __attribute__((cdecl))")]
        public IntPtr size;

        [NativeTypeName("Sint64 (*)(struct SDL_RWops *, Sint64, int) __attribute__((cdecl))")]
        public IntPtr seek;

        [NativeTypeName("size_t (*)(struct SDL_RWops *, void *, size_t, size_t) __attribute__((cdecl))")]
        public IntPtr read;

        [NativeTypeName("size_t (*)(struct SDL_RWops *, const void *, size_t, size_t) __attribute__((cdecl))")]
        public IntPtr write;

        [NativeTypeName("int (*)(struct SDL_RWops *) __attribute__((cdecl))")]
        public IntPtr close;

        [NativeTypeName("Uint32")]
        public uint type;

        [NativeTypeName("__AnonymousRecord_SDL_rwops_L96_C5")]
        public _hidden_e__Union hidden;

        [StructLayout(LayoutKind.Explicit)]
        public partial struct _hidden_e__Union
        {
            [FieldOffset(0)]
            [NativeTypeName("__AnonymousRecord_SDL_rwops_L104_C9")]
            public _windowsio_e__Struct windowsio;

            [FieldOffset(0)]
            [NativeTypeName("__AnonymousRecord_SDL_rwops_L118_C9")]
            public _stdio_e__Struct stdio;

            [FieldOffset(0)]
            [NativeTypeName("__AnonymousRecord_SDL_rwops_L124_C9")]
            public _mem_e__Struct mem;

            [FieldOffset(0)]
            [NativeTypeName("__AnonymousRecord_SDL_rwops_L130_C9")]
            public _unknown_e__Struct unknown;

            public partial struct _windowsio_e__Struct
            {
                public SDL_bool append;

                [NativeTypeName("void*")]
                public nint h;

                [NativeTypeName("__AnonymousRecord_SDL_rwops_L108_C13")]
                public _buffer_e__Struct buffer;

                public partial struct _buffer_e__Struct
                {
                    [NativeTypeName("void*")]
                    public nint data;

                    [NativeTypeName("size_t")]
                    public UIntPtr size;

                    [NativeTypeName("size_t")]
                    public UIntPtr left;
                }
            }

            public partial struct _stdio_e__Struct
            {
                public SDL_bool autoclose;

                [NativeTypeName("FILE *")]
                public nint fp;
            }

            public unsafe partial struct _mem_e__Struct
            {
                [NativeTypeName("Uint8 *")]
                public byte* @base;

                [NativeTypeName("Uint8 *")]
                public byte* here;

                [NativeTypeName("Uint8 *")]
                public byte* stop;
            }

            public partial struct _unknown_e__Struct
            {
                [NativeTypeName("void*")]
                public nint data1;

                [NativeTypeName("void*")]
                public nint data2;
            }
        }
    }

    internal static unsafe partial class SDLNative
    {
        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_RWops* SDL_RWFromFile([NativeTypeName("const char *")] byte* file, [NativeTypeName("const char *")] byte* mode);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_RWops* SDL_RWFromMem([NativeTypeName("void*")] nint mem, int size);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_RWops* SDL_RWFromConstMem([NativeTypeName("const void *")] nint mem, int size);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_RWops* SDL_AllocRW();

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_FreeRW(SDL_RWops* area);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("Sint64")]
        public static extern long SDL_RWsize(SDL_RWops* context);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("Sint64")]
        public static extern long SDL_RWseek(SDL_RWops* context, [NativeTypeName("Sint64")] long offset, int whence);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("Sint64")]
        public static extern long SDL_RWtell(SDL_RWops* context);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("size_t")]
        public static extern UIntPtr SDL_RWread(SDL_RWops* context, [NativeTypeName("void*")] nint ptr, [NativeTypeName("size_t")] UIntPtr size, [NativeTypeName("size_t")] UIntPtr maxnum);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("size_t")]
        public static extern UIntPtr SDL_RWwrite(SDL_RWops* context, [NativeTypeName("const void *")] nint ptr, [NativeTypeName("size_t")] UIntPtr size, [NativeTypeName("size_t")] UIntPtr num);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_RWclose(SDL_RWops* context);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("void*")]
        public static extern nint SDL_LoadFile_RW(SDL_RWops* src, [NativeTypeName("size_t *")] UIntPtr* datasize, int freesrc);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("void*")]
        public static extern nint SDL_LoadFile([NativeTypeName("const char *")] byte* file, [NativeTypeName("size_t *")] UIntPtr* datasize);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("Uint8")]
        public static extern byte SDL_ReadU8(SDL_RWops* src);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("Uint16")]
        public static extern ushort SDL_ReadLE16(SDL_RWops* src);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("Uint16")]
        public static extern ushort SDL_ReadBE16(SDL_RWops* src);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("Uint32")]
        public static extern uint SDL_ReadLE32(SDL_RWops* src);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("Uint32")]
        public static extern uint SDL_ReadBE32(SDL_RWops* src);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("Uint64")]
        public static extern ulong SDL_ReadLE64(SDL_RWops* src);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("Uint64")]
        public static extern ulong SDL_ReadBE64(SDL_RWops* src);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("size_t")]
        public static extern UIntPtr SDL_WriteU8(SDL_RWops* dst, [NativeTypeName("Uint8")] byte value);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("size_t")]
        public static extern UIntPtr SDL_WriteLE16(SDL_RWops* dst, [NativeTypeName("Uint16")] ushort value);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("size_t")]
        public static extern UIntPtr SDL_WriteBE16(SDL_RWops* dst, [NativeTypeName("Uint16")] ushort value);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("size_t")]
        public static extern UIntPtr SDL_WriteLE32(SDL_RWops* dst, [NativeTypeName("Uint32")] uint value);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("size_t")]
        public static extern UIntPtr SDL_WriteBE32(SDL_RWops* dst, [NativeTypeName("Uint32")] uint value);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("size_t")]
        public static extern UIntPtr SDL_WriteLE64(SDL_RWops* dst, [NativeTypeName("Uint64")] ulong value);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("size_t")]
        public static extern UIntPtr SDL_WriteBE64(SDL_RWops* dst, [NativeTypeName("Uint64")] ulong value);

        [NativeTypeName("#define SDL_RWOPS_UNKNOWN 0U")]
        public const uint SDL_RWOPS_UNKNOWN = 0U;

        [NativeTypeName("#define SDL_RWOPS_WINFILE 1U")]
        public const uint SDL_RWOPS_WINFILE = 1U;

        [NativeTypeName("#define SDL_RWOPS_STDFILE 2U")]
        public const uint SDL_RWOPS_STDFILE = 2U;

        [NativeTypeName("#define SDL_RWOPS_JNIFILE 3U")]
        public const uint SDL_RWOPS_JNIFILE = 3U;

        [NativeTypeName("#define SDL_RWOPS_MEMORY 4U")]
        public const uint SDL_RWOPS_MEMORY = 4U;

        [NativeTypeName("#define SDL_RWOPS_MEMORY_RO 5U")]
        public const uint SDL_RWOPS_MEMORY_RO = 5U;

        [NativeTypeName("#define RW_SEEK_SET 0")]
        public const int RW_SEEK_SET = 0;

        [NativeTypeName("#define RW_SEEK_CUR 1")]
        public const int RW_SEEK_CUR = 1;

        [NativeTypeName("#define RW_SEEK_END 2")]
        public const int RW_SEEK_END = 2;
    }
}
