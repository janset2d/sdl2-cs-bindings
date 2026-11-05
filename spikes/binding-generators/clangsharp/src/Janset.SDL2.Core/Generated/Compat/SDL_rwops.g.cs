using System;
using System.Runtime.InteropServices;

namespace SDL2
{

    internal static unsafe partial class SDLNative
    {
        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_RWops SDL_RWFromFile([NativeTypeName("const char *")] byte* file, [NativeTypeName("const char *")] byte* mode);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_RWops SDL_RWFromMem([NativeTypeName("void*")] nint mem, int size);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_RWops SDL_RWFromConstMem([NativeTypeName("const void *")] nint mem, int size);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_RWops SDL_AllocRW();

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_FreeRW(SDL_RWops area);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("Sint64")]
        public static extern long SDL_RWsize(SDL_RWops context);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("Sint64")]
        public static extern long SDL_RWseek(SDL_RWops context, [NativeTypeName("Sint64")] long offset, int whence);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("Sint64")]
        public static extern long SDL_RWtell(SDL_RWops context);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("size_t")]
        public static extern UIntPtr SDL_RWread(SDL_RWops context, [NativeTypeName("void*")] nint ptr, [NativeTypeName("size_t")] UIntPtr size, [NativeTypeName("size_t")] UIntPtr maxnum);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("size_t")]
        public static extern UIntPtr SDL_RWwrite(SDL_RWops context, [NativeTypeName("const void *")] nint ptr, [NativeTypeName("size_t")] UIntPtr size, [NativeTypeName("size_t")] UIntPtr num);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_RWclose(SDL_RWops context);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("void*")]
        public static extern nint SDL_LoadFile_RW(SDL_RWops src, [NativeTypeName("size_t *")] UIntPtr* datasize, int freesrc);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("void*")]
        public static extern nint SDL_LoadFile([NativeTypeName("const char *")] byte* file, [NativeTypeName("size_t *")] UIntPtr* datasize);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("Uint8")]
        public static extern byte SDL_ReadU8(SDL_RWops src);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("Uint16")]
        public static extern ushort SDL_ReadLE16(SDL_RWops src);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("Uint16")]
        public static extern ushort SDL_ReadBE16(SDL_RWops src);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("Uint32")]
        public static extern uint SDL_ReadLE32(SDL_RWops src);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("Uint32")]
        public static extern uint SDL_ReadBE32(SDL_RWops src);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("Uint64")]
        public static extern ulong SDL_ReadLE64(SDL_RWops src);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("Uint64")]
        public static extern ulong SDL_ReadBE64(SDL_RWops src);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("size_t")]
        public static extern UIntPtr SDL_WriteU8(SDL_RWops dst, [NativeTypeName("Uint8")] byte value);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("size_t")]
        public static extern UIntPtr SDL_WriteLE16(SDL_RWops dst, [NativeTypeName("Uint16")] ushort value);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("size_t")]
        public static extern UIntPtr SDL_WriteBE16(SDL_RWops dst, [NativeTypeName("Uint16")] ushort value);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("size_t")]
        public static extern UIntPtr SDL_WriteLE32(SDL_RWops dst, [NativeTypeName("Uint32")] uint value);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("size_t")]
        public static extern UIntPtr SDL_WriteBE32(SDL_RWops dst, [NativeTypeName("Uint32")] uint value);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("size_t")]
        public static extern UIntPtr SDL_WriteLE64(SDL_RWops dst, [NativeTypeName("Uint64")] ulong value);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("size_t")]
        public static extern UIntPtr SDL_WriteBE64(SDL_RWops dst, [NativeTypeName("Uint64")] ulong value);

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
