using Build.Targets.GenerateBindings.Model;

namespace Build.Targets.GenerateBindings;

internal sealed record Sdl2CoreGenerationConfig(
    string Family,
    string LibraryName,
    string Namespace,
    string PrimaryClassName,
    IReadOnlyList<string> OwnedPrefixes,
    IReadOnlyList<string> DeferredDeclarations,
    IReadOnlySet<string> ExcludedFunctionNames,
    IReadOnlyList<PreviewFunction> RequiredFunctions)
{
    public static Sdl2CoreGenerationConfig Default { get; } = new(
        Family: "sdl2-core",
        LibraryName: "SDL2",
        Namespace: "SDL2",
        PrimaryClassName: "SDL",
        OwnedPrefixes: ["SDL_", "SDLK_", "SDL_HINT_", "SDL_INIT_"],
        DeferredDeclarations:
        [
            "SDL_SysWMinfo",
            "SDL_SysWMmsg",
        ],
        // SDL_main is declared in SDL_main.h as `extern DECLSPEC int SDL_main(int argc, char *argv[])`,
        // but its body is defined by the consuming application, not by SDL2 itself. SDL2's
        // libSDL2main.a / SDLmain.lib is a static-link wrapper that bridges into the application's
        // SDL_main. The runtime SDL2.dll / libSDL2-2.0.so never exports this symbol — a P/Invoke
        // against it would always raise EntryPointNotFoundException at consumer runtime.
        //
        // SDL_DYNAPI_entry is SDL2's dynamic-API bootstrap handler — declared in SDL_dynapi.c
        // (a C source file, not a public header) and listed in the dynapi manifest because the
        // runtime SDL2 library exports it for its own loader. Consumer bindings should never
        // call it; treating it as a public P/Invoke target is meaningless. Filtering it from
        // emit also clears the only legitimate manifest-side "missing" warning from the dynapi
        // cross-check validator under the Stage1Generator severity profile.
        ExcludedFunctionNames: new HashSet<string>(StringComparer.Ordinal)
        {
            "SDL_main",
            "SDL_DYNAPI_entry",
        },
        // Hand-curated P/Invoke declarations for SDL2 public functions the per-header
        // parse loop cannot reach. SDL.h is the umbrella header that #includes every
        // other SDL2 header; the resolver excludes it because per-header parsing of
        // an umbrella collapses into one giant translation unit and defeats isolation.
        // SDL.h is also a declarator in its own right: it declares 5 base API functions
        // (SDL_Init, SDL_InitSubSystem, SDL_QuitSubSystem, SDL_WasInit, SDL_Quit) that
        // exist nowhere else in the SDL2 source tree. The translator merges this list
        // into the Neutral view's emission set so consumers see the canonical
        // initialise/shutdown surface.
        //
        // Maintenance: on every SDL2 minor version bump, diff SDL.h's `extern DECLSPEC`
        // declarations against this list to catch new base functions (or removed ones).
        // SDL2's base API has been stable since 2.0.0 — these 5 entries haven't changed
        // through the entire 2.x line.
        RequiredFunctions:
        [
            new PreviewFunction(
                Name: "SDL_Init",
                ReturnType: "int",
                Parameters: [new PreviewParameter("uint", "flags")],
                SourceHeader: "SDL.h"),
            new PreviewFunction(
                Name: "SDL_InitSubSystem",
                ReturnType: "int",
                Parameters: [new PreviewParameter("uint", "flags")],
                SourceHeader: "SDL.h"),
            new PreviewFunction(
                Name: "SDL_QuitSubSystem",
                ReturnType: "void",
                Parameters: [new PreviewParameter("uint", "flags")],
                SourceHeader: "SDL.h"),
            new PreviewFunction(
                Name: "SDL_WasInit",
                ReturnType: "uint",
                Parameters: [new PreviewParameter("uint", "flags")],
                SourceHeader: "SDL.h"),
            new PreviewFunction(
                Name: "SDL_Quit",
                ReturnType: "void",
                Parameters: [],
                SourceHeader: "SDL.h"),
        ]);
}
