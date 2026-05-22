using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SDL2
{
    public partial struct SDL_GUID
    {
        [NativeTypeName("Uint8[16]")]
        public _data_e__FixedBuffer data;

        [InlineArray(16)]
        public partial struct _data_e__FixedBuffer
        {
            public byte e0;
        }
    }

    internal static unsafe partial class SDLNative
    {
        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_GUIDToString(SDL_GUID guid, [NativeTypeName("char *")] byte* pszGUID, int cbGUID);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_GUID SDL_GUIDFromString([NativeTypeName("const char *")] byte* pchGUID);
    }
}
