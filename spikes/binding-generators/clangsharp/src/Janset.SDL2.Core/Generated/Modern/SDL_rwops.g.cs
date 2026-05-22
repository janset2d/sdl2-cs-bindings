using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace SDL2
{
    public unsafe partial struct SDL_RWops
    {
        [NativeTypeName("Sint64 (*)(struct SDL_RWops *) __attribute__((cdecl))")]
        public delegate* unmanaged[Cdecl]<SDL_RWops*, long> size;

        [NativeTypeName("Sint64 (*)(struct SDL_RWops *, Sint64, int) __attribute__((cdecl))")]
        public delegate* unmanaged[Cdecl]<SDL_RWops*, long, int, long> seek;

        [NativeTypeName("size_t (*)(struct SDL_RWops *, void *, size_t, size_t) __attribute__((cdecl))")]
        public delegate* unmanaged[Cdecl]<SDL_RWops*, nint, nuint, nuint, nuint> read;

        [NativeTypeName("size_t (*)(struct SDL_RWops *, const void *, size_t, size_t) __attribute__((cdecl))")]
        public delegate* unmanaged[Cdecl]<SDL_RWops*, nint, nuint, nuint, nuint> write;

        [NativeTypeName("int (*)(struct SDL_RWops *) __attribute__((cdecl))")]
        public delegate* unmanaged[Cdecl]<SDL_RWops*, int> close;

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
                    public nuint size;

                    [NativeTypeName("size_t")]
                    public nuint left;
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

    public static unsafe partial class SDLNative
    {
        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_RWops* SDL_RWFromFile([NativeTypeName("const char *")] byte* file, [NativeTypeName("const char *")] byte* mode);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_RWops* SDL_RWFromMem([NativeTypeName("void*")] nint mem, int size);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_RWops* SDL_RWFromConstMem([NativeTypeName("const void *")] nint mem, int size);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_RWops* SDL_AllocRW();

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_FreeRW(SDL_RWops* area);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("Sint64")]
        public static partial long SDL_RWsize(SDL_RWops* context);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("Sint64")]
        public static partial long SDL_RWseek(SDL_RWops* context, [NativeTypeName("Sint64")] long offset, int whence);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("Sint64")]
        public static partial long SDL_RWtell(SDL_RWops* context);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("size_t")]
        public static partial nuint SDL_RWread(SDL_RWops* context, [NativeTypeName("void*")] nint ptr, [NativeTypeName("size_t")] nuint size, [NativeTypeName("size_t")] nuint maxnum);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("size_t")]
        public static partial nuint SDL_RWwrite(SDL_RWops* context, [NativeTypeName("const void *")] nint ptr, [NativeTypeName("size_t")] nuint size, [NativeTypeName("size_t")] nuint num);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_RWclose(SDL_RWops* context);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("void*")]
        public static partial nint SDL_LoadFile_RW(SDL_RWops* src, [NativeTypeName("size_t *")] nuint* datasize, int freesrc);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("void*")]
        public static partial nint SDL_LoadFile([NativeTypeName("const char *")] byte* file, [NativeTypeName("size_t *")] nuint* datasize);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("Uint8")]
        public static partial byte SDL_ReadU8(SDL_RWops* src);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("Uint16")]
        public static partial ushort SDL_ReadLE16(SDL_RWops* src);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("Uint16")]
        public static partial ushort SDL_ReadBE16(SDL_RWops* src);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("Uint32")]
        public static partial uint SDL_ReadLE32(SDL_RWops* src);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("Uint32")]
        public static partial uint SDL_ReadBE32(SDL_RWops* src);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("Uint64")]
        public static partial ulong SDL_ReadLE64(SDL_RWops* src);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("Uint64")]
        public static partial ulong SDL_ReadBE64(SDL_RWops* src);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("size_t")]
        public static partial nuint SDL_WriteU8(SDL_RWops* dst, [NativeTypeName("Uint8")] byte value);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("size_t")]
        public static partial nuint SDL_WriteLE16(SDL_RWops* dst, [NativeTypeName("Uint16")] ushort value);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("size_t")]
        public static partial nuint SDL_WriteBE16(SDL_RWops* dst, [NativeTypeName("Uint16")] ushort value);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("size_t")]
        public static partial nuint SDL_WriteLE32(SDL_RWops* dst, [NativeTypeName("Uint32")] uint value);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("size_t")]
        public static partial nuint SDL_WriteBE32(SDL_RWops* dst, [NativeTypeName("Uint32")] uint value);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("size_t")]
        public static partial nuint SDL_WriteLE64(SDL_RWops* dst, [NativeTypeName("Uint64")] ulong value);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("size_t")]
        public static partial nuint SDL_WriteBE64(SDL_RWops* dst, [NativeTypeName("Uint64")] ulong value);

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
