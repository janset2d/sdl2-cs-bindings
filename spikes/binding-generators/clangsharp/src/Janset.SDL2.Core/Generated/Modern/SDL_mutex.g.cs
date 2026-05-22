using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace SDL2
{
    public partial struct SDL_mutex
    {
    }

    public partial struct SDL_semaphore
    {
    }

    public partial struct SDL_cond
    {
    }

    public static unsafe partial class SDLNative
    {
        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_mutex* SDL_CreateMutex();

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_LockMutex(SDL_mutex* mutex);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_TryLockMutex(SDL_mutex* mutex);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_UnlockMutex(SDL_mutex* mutex);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_DestroyMutex(SDL_mutex* mutex);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("SDL_sem *")]
        public static partial SDL_semaphore* SDL_CreateSemaphore([NativeTypeName("Uint32")] uint initial_value);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_DestroySemaphore([NativeTypeName("SDL_sem *")] SDL_semaphore* sem);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_SemWait([NativeTypeName("SDL_sem *")] SDL_semaphore* sem);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_SemTryWait([NativeTypeName("SDL_sem *")] SDL_semaphore* sem);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_SemWaitTimeout([NativeTypeName("SDL_sem *")] SDL_semaphore* sem, [NativeTypeName("Uint32")] uint timeout);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_SemPost([NativeTypeName("SDL_sem *")] SDL_semaphore* sem);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [return: NativeTypeName("Uint32")]
        public static partial uint SDL_SemValue([NativeTypeName("SDL_sem *")] SDL_semaphore* sem);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial SDL_cond* SDL_CreateCond();

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial void SDL_DestroyCond(SDL_cond* cond);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_CondSignal(SDL_cond* cond);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_CondBroadcast(SDL_cond* cond);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_CondWait(SDL_cond* cond, SDL_mutex* mutex);

        [LibraryImport("SDL2")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int SDL_CondWaitTimeout(SDL_cond* cond, SDL_mutex* mutex, [NativeTypeName("Uint32")] uint ms);

        [NativeTypeName("#define SDL_MUTEX_TIMEDOUT 1")]
        public const int SDL_MUTEX_TIMEDOUT = 1;

        [NativeTypeName("#define SDL_MUTEX_MAXWAIT (~(Uint32)0)")]
        public const uint SDL_MUTEX_MAXWAIT = (~(uint)(0));
    }
}
