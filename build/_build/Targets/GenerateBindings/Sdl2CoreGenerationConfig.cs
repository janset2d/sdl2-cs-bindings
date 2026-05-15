namespace Build.Targets.GenerateBindings;

internal sealed record Sdl2CoreGenerationConfig(
    string Family,
    string LibraryName,
    string Namespace,
    string PrimaryClassName,
    IReadOnlyList<string> OwnedPrefixes,
    IReadOnlyList<string> DeferredDeclarations)
{
    public static Sdl2CoreGenerationConfig Default { get; } = new(
        Family: "sdl2-core",
        LibraryName: "SDL2",
        Namespace: "SDL2",
        PrimaryClassName: "SDL",
        OwnedPrefixes: ["SDL_", "SDLK_", "SDL_HINT_", "SDL_INIT_"],
        DeferredDeclarations:
        [
            // These SDL_syswm declarations require platform-specific union layout support.
            "SDL_SysWMinfo",
            "SDL_SysWMmsg",
        ]);
}
