using System;
using System.Runtime.InteropServices;

namespace SDL2
{
    public partial struct SDL_Thread
    {
    }

    public enum SDL_ThreadPriority
    {
        SDL_THREAD_PRIORITY_LOW,
        SDL_THREAD_PRIORITY_NORMAL,
        SDL_THREAD_PRIORITY_HIGH,
        SDL_THREAD_PRIORITY_TIME_CRITICAL,
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int SDL_ThreadFunction([NativeTypeName("void*")] nint data);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: NativeTypeName("uintptr_t")]
    public unsafe delegate UIntPtr pfnSDL_CurrentBeginThread([NativeTypeName("void*")] nint param0, [NativeTypeName("unsigned int")] uint param1, [NativeTypeName("unsigned int (*)(void *) __attribute__((stdcall))")] IntPtr func, [NativeTypeName("void*")] nint param3, [NativeTypeName("unsigned int")] uint param4, [NativeTypeName("unsigned int *")] uint* param5);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void pfnSDL_CurrentEndThread([NativeTypeName("unsigned int")] uint code);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void SDL_TLSDestructorCallback([NativeTypeName("void*")] nint param0);

    internal static unsafe partial class SDLNative
    {
        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("const char *")]
        public static extern byte* SDL_GetThreadName(SDL_Thread* thread);

        [return: NativeTypeName("SDL_threadID")]
        public static ulong SDL_ThreadID()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return SDL_ThreadID_Win32();
            return (ulong)SDL_ThreadID_Unix64();
        }

        [DllImport("SDL2", EntryPoint = "SDL_ThreadID", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern uint SDL_ThreadID_Win32();

        [DllImport("SDL2", EntryPoint = "SDL_ThreadID", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern nint SDL_ThreadID_Unix64();

        [return: NativeTypeName("SDL_threadID")]
        public static ulong SDL_GetThreadID(SDL_Thread* thread)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return SDL_GetThreadID_Win32(thread);
            return (ulong)SDL_GetThreadID_Unix64(thread);
        }

        [DllImport("SDL2", EntryPoint = "SDL_GetThreadID", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern uint SDL_GetThreadID_Win32(SDL_Thread* thread);

        [DllImport("SDL2", EntryPoint = "SDL_GetThreadID", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern nint SDL_GetThreadID_Unix64(SDL_Thread* thread);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_SetThreadPriority(SDL_ThreadPriority priority);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_WaitThread(SDL_Thread* thread, int* status);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_DetachThread(SDL_Thread* thread);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("SDL_TLSID")]
        public static extern uint SDL_TLSCreate();

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("void*")]
        public static extern nint SDL_TLSGet([NativeTypeName("SDL_TLSID")] uint id);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_TLSSet([NativeTypeName("SDL_TLSID")] uint id, [NativeTypeName("const void *")] nint value, [NativeTypeName("SDL_TLSDestructorCallback")] IntPtr destructor);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_TLSCleanup();
    }
}
