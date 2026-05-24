using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace SDL2
{

    public enum SDL_ThreadPriority
    {
        SDL_THREAD_PRIORITY_LOW,
        SDL_THREAD_PRIORITY_NORMAL,
        SDL_THREAD_PRIORITY_HIGH,
        SDL_THREAD_PRIORITY_TIME_CRITICAL,
    }

    internal static unsafe partial class SDLNative
    {
        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("const char *")]
        public static partial byte* SDL_GetThreadName(SDL_Thread thread);

        [LibraryImport("SDL2", EntryPoint = "SDL_ThreadID")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("SDL_threadID")]
        public static partial CULong SDL_ThreadID();

        [LibraryImport("SDL2", EntryPoint = "SDL_GetThreadID")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("SDL_threadID")]
        public static partial CULong SDL_GetThreadID(SDL_Thread thread);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_SetThreadPriority(SDL_ThreadPriority priority);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_WaitThread(SDL_Thread thread, int* status);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_DetachThread(SDL_Thread thread);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("SDL_TLSID")]
        public static partial uint SDL_TLSCreate();

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("void*")]
        public static partial nint SDL_TLSGet([NativeTypeName("SDL_TLSID")] uint id);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_TLSSet([NativeTypeName("SDL_TLSID")] uint id, [NativeTypeName("const void *")] nint value, [NativeTypeName("SDL_TLSDestructorCallback")] delegate* unmanaged[Cdecl]<nint, void> destructor);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_TLSCleanup();
    }
}
