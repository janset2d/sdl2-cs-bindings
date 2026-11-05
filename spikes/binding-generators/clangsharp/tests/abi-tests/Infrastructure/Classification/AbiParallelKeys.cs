namespace Janset.SDL2.AbiTests.Infrastructure.Classification;

internal static class AbiParallelKeys
{
    public const string Error = "SDL.Error";

    // SDL_InitSubSystem, SDL_QuitSubSystem and hints are process-wide state, so tests that
    // touch them share one key instead of one per subsystem.
    public const string SdlGlobalState = "SDL.GlobalState";
}
