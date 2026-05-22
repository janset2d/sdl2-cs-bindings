using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace SDL2
{
    public static unsafe partial class SDLNative
    {
        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_RecordGesture([NativeTypeName("SDL_TouchID")] long touchId);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_SaveAllDollarTemplates(SDL_RWops* dst);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_SaveDollarTemplate([NativeTypeName("SDL_GestureID")] long gestureId, SDL_RWops* dst);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_LoadDollarTemplates([NativeTypeName("SDL_TouchID")] long touchId, SDL_RWops* src);
    }
}
