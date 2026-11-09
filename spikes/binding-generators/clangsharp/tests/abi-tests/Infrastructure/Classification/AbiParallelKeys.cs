namespace Janset.SDL2.AbiTests.Infrastructure.Classification;

internal static class AbiParallelKeys
{
    public const string Error = "SDL.Error";

    /// <summary>Subsystem lifecycles and hints share process-wide state.</summary>
    public const string SdlGlobalState = "SDL.GlobalState";
}
