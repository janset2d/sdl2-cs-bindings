using System.Runtime.InteropServices;

namespace SDL2
{
    public partial struct SDL_atomic_t
    {
        public int value;
    }

    internal static unsafe partial class SDLNative
    {
        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_bool SDL_AtomicTryLock([NativeTypeName("SDL_SpinLock *")] int* @lock);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_AtomicLock([NativeTypeName("SDL_SpinLock *")] int* @lock);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_AtomicUnlock([NativeTypeName("SDL_SpinLock *")] int* @lock);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_MemoryBarrierReleaseFunction();

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern void SDL_MemoryBarrierAcquireFunction();

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_bool SDL_AtomicCAS(SDL_atomic_t* a, int oldval, int newval);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_AtomicSet(SDL_atomic_t* a, int v);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_AtomicGet(SDL_atomic_t* a);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern int SDL_AtomicAdd(SDL_atomic_t* a, int v);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        public static extern SDL_bool SDL_AtomicCASPtr([NativeTypeName("void **")] nint* a, [NativeTypeName("void*")] nint oldval, [NativeTypeName("void*")] nint newval);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("void*")]
        public static extern nint SDL_AtomicSetPtr([NativeTypeName("void **")] nint* a, [NativeTypeName("void*")] nint v);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
        [return: NativeTypeName("void*")]
        public static extern nint SDL_AtomicGetPtr([NativeTypeName("void **")] nint* a);
    }
}
