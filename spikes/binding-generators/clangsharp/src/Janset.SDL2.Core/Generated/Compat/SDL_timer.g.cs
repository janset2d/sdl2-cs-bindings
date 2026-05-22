using System;
using System.Runtime.InteropServices;

namespace SDL2
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: NativeTypeName("Uint32")]
    public delegate uint SDL_TimerCallback([NativeTypeName("Uint32")] uint interval, [NativeTypeName("void*")] nint param1);

    public static partial class SDLNative
    {
        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("Uint32")]
        public static extern uint SDL_GetTicks();

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("Uint64")]
        public static extern ulong SDL_GetTicks64();

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("Uint64")]
        public static extern ulong SDL_GetPerformanceCounter();

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("Uint64")]
        public static extern ulong SDL_GetPerformanceFrequency();

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_Delay([NativeTypeName("Uint32")] uint ms);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("SDL_TimerID")]
        public static extern int SDL_AddTimer([NativeTypeName("Uint32")] uint interval, [NativeTypeName("SDL_TimerCallback")] IntPtr callback, [NativeTypeName("void*")] nint param2);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_bool SDL_RemoveTimer([NativeTypeName("SDL_TimerID")] int id);
    }
}
