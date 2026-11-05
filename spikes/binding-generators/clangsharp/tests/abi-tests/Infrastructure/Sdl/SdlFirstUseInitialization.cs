using SDL2;

namespace Janset.SDL2.AbiTests.Infrastructure.Sdl;

public static class SdlFirstUseInitialization
{
    // SDL2 sets up two pieces of process-wide state lazily and without a lock on first use:
    // - the tick counter: SDL_TicksInit marks it started before it reads the performance-counter
    //   frequency, so a thread that reads the ticks in that window (SDL_PushEvent timestamps every
    //   event) divides by zero;
    // - the error-buffer thread-local slot: two first callers of SDL_GetErrBuf can each allocate the
    //   shared TLS index and overwrite each other's.
    // Applications avoid both by initializing SDL on one thread first; the suite does the same once,
    // before tests run in parallel.
    [Before(HookType.Assembly)]
    public static void InitializeLazyProcessState(AssemblyHookContext context)
    {
        _ = SDLNative.SDL_GetTicks64();
        SDLNative.SDL_ClearError();
    }
}
