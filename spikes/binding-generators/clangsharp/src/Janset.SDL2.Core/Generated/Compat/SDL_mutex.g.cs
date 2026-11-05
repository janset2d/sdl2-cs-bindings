using System.Runtime.InteropServices;

namespace SDL2
{

    internal static unsafe partial class SDLNative
    {
        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_mutex SDL_CreateMutex();

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_LockMutex(SDL_mutex mutex);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_TryLockMutex(SDL_mutex mutex);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_UnlockMutex(SDL_mutex mutex);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_DestroyMutex(SDL_mutex mutex);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_sem SDL_CreateSemaphore([NativeTypeName("Uint32")] uint initial_value);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_DestroySemaphore(SDL_sem sem);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_SemWait(SDL_sem sem);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_SemTryWait(SDL_sem sem);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_SemWaitTimeout(SDL_sem sem, [NativeTypeName("Uint32")] uint timeout);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_SemPost(SDL_sem sem);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("Uint32")]
        public static extern uint SDL_SemValue(SDL_sem sem);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_cond SDL_CreateCond();

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_DestroyCond(SDL_cond cond);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_CondSignal(SDL_cond cond);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_CondBroadcast(SDL_cond cond);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_CondWait(SDL_cond cond, SDL_mutex mutex);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_CondWaitTimeout(SDL_cond cond, SDL_mutex mutex, [NativeTypeName("Uint32")] uint ms);

        [NativeTypeName("#define SDL_MUTEX_TIMEDOUT 1")]
        public const int SDL_MUTEX_TIMEDOUT = 1;

        [NativeTypeName("#define SDL_MUTEX_MAXWAIT (~(Uint32)0)")]
        public const uint SDL_MUTEX_MAXWAIT = (~(uint)(0));
    }
}
