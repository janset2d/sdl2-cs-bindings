namespace Janset.SDL2.AbiTests.Infrastructure.Classification;

internal static class AbiParallelKeys
{
    public const string Error = "SDL.Error";

    // Subsystem init/quit and hints share process-wide SDL state.
    public const string SdlGlobalState = "SDL.GlobalState";
}
