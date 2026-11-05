namespace Janset.SDL2.AbiTests.Infrastructure.Classification;

internal static class AbiParallelKeys
{
    public const string Error = "SDL.Error";

    // SDL_InitSubSystem, SDL_QuitSubSystem, hints and the SDL_*DRIVER environment variables are
    // process-wide state, so every test that touches any of them shares one key instead of one per
    // subsystem.
    public const string SdlGlobalState = "SDL.GlobalState";
}
