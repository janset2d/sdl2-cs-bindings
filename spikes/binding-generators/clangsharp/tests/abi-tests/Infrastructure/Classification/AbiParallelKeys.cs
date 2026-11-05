namespace Janset.SDL2.AbiTests.Infrastructure.Classification;

internal static class AbiParallelKeys
{
    public const string Error = "SDL.Error";

    // SDL_InitSubSystem, SDL_QuitSubSystem, hints and the process environment are shared state.
    // Tests that write the environment and tests whose SDL calls read it or the hint list must
    // share this key, along with subsystem lifecycle tests.
    public const string SdlGlobalState = "SDL.GlobalState";
}
