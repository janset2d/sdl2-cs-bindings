using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace SDL2
{
    public static unsafe partial class SDLNative
    {
        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("Uint32")]
        public static partial uint SDL_GetTicks();

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("Uint64")]
        public static partial ulong SDL_GetTicks64();

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("Uint64")]
        public static partial ulong SDL_GetPerformanceCounter();

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("Uint64")]
        public static partial ulong SDL_GetPerformanceFrequency();

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_Delay([NativeTypeName("Uint32")] uint ms);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("SDL_TimerID")]
        public static partial int SDL_AddTimer([NativeTypeName("Uint32")] uint interval, [NativeTypeName("SDL_TimerCallback")] delegate* unmanaged[Cdecl]<uint, nint, uint> callback, [NativeTypeName("void*")] nint param2);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_bool SDL_RemoveTimer([NativeTypeName("SDL_TimerID")] int id);
    }
}
