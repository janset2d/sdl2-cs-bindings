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

#if NET6_0_OR_GREATER
        [global::System.Runtime.InteropServices.LibraryImport("SDL2", EntryPoint = "SDL_ThreadID")]
        [global::System.Runtime.InteropServices.UnmanagedCallConv(CallConvs = new[] { typeof(global::System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("SDL_threadID")]
        public static partial global::System.Runtime.InteropServices.CULong SDL_ThreadID();
#endif
#if !NET6_0_OR_GREATER
        [return: NativeTypeName("SDL_threadID")]
        public static ulong SDL_ThreadID()
        {
            if (global::System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(global::System.Runtime.InteropServices.OSPlatform.Windows))
                return SDL_ThreadID_Win32();
            return (ulong)SDL_ThreadID_Unix64();
        }

        [global::System.Runtime.InteropServices.DllImport("SDL2", EntryPoint = "SDL_ThreadID", CallingConvention = global::System.Runtime.InteropServices.CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern uint SDL_ThreadID_Win32();

        [global::System.Runtime.InteropServices.DllImport("SDL2", EntryPoint = "SDL_ThreadID", CallingConvention = global::System.Runtime.InteropServices.CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern nint SDL_ThreadID_Unix64();
#endif

#if NET6_0_OR_GREATER
        [global::System.Runtime.InteropServices.LibraryImport("SDL2", EntryPoint = "SDL_GetThreadID")]
        [global::System.Runtime.InteropServices.UnmanagedCallConv(CallConvs = new[] { typeof(global::System.Runtime.CompilerServices.CallConvCdecl) })]
        [return: NativeTypeName("SDL_threadID")]
        public static partial global::System.Runtime.InteropServices.CULong SDL_GetThreadID(SDL_Thread* thread);
#endif
#if !NET6_0_OR_GREATER
        [return: NativeTypeName("SDL_threadID")]
        public static ulong SDL_GetThreadID(SDL_Thread* thread)
        {
            if (global::System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(global::System.Runtime.InteropServices.OSPlatform.Windows))
                return SDL_GetThreadID_Win32(thread);
            return (ulong)SDL_GetThreadID_Unix64(thread);
        }

        [global::System.Runtime.InteropServices.DllImport("SDL2", EntryPoint = "SDL_GetThreadID", CallingConvention = global::System.Runtime.InteropServices.CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern uint SDL_GetThreadID_Win32(SDL_Thread* thread);

        [global::System.Runtime.InteropServices.DllImport("SDL2", EntryPoint = "SDL_GetThreadID", CallingConvention = global::System.Runtime.InteropServices.CallingConvention.Cdecl, ExactSpelling = true)]
        private static extern nint SDL_GetThreadID_Unix64(SDL_Thread* thread);
#endif

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
