using SDL2;

namespace Janset.SDL2.AbiTests.Infrastructure.Sdl;

public static class SdlTimerInitialization
{
    // SDL2's first tick-counter setup is not thread-safe: SDL_TicksInit marks the counter as started
    // before it reads the performance-counter frequency, so a thread that reads the ticks in that
    // window (SDL_PushEvent timestamps every event) divides by zero. Applications avoid it by
    // initializing SDL on one thread first; the suite does the same once, before tests run in parallel.
    [Before(HookType.Assembly)]
    public static void InitializeTickCounter(AssemblyHookContext context)
    {
        _ = SDLNative.SDL_GetTicks64();
    }
}
