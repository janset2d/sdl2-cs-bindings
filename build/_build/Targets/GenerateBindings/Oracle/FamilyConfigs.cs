internal static class FamilyConfigs
{
    public static readonly FamilyConfig Sdl2Core = new(
        "sdl2-core",
        "SDL2 Core",
        "SDL2",
        "SDLNative",
        "artifacts/generated-bindings-preview/sdl2-core",
        "spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Compat",
        "spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Modern",
        "external/sdl2-cs/src/SDL2.cs",
        true);

    public static readonly FamilyConfig Sdl2Image = new(
        "sdl2-image",
        "SDL2 Image",
        "SDL2.Image",
        "SDL_imageNative",
        null,
        "spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Generated/Compat",
        "spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Generated/Modern",
        "external/sdl2-cs/src/SDL2_image.cs",
        false);

    public static readonly FamilyConfig Sdl2Ttf = new(
        "sdl2-ttf",
        "SDL2 TTF",
        "SDL2.Ttf",
        "SDL_ttfNative",
        null,
        "spikes/binding-generators/clangsharp/src/Janset.SDL2.Ttf/Generated/Compat",
        "spikes/binding-generators/clangsharp/src/Janset.SDL2.Ttf/Generated/Modern",
        "external/sdl2-cs/src/SDL2_ttf.cs",
        false);

    public static readonly FamilyConfig Sdl2Mixer = new(
        "sdl2-mixer",
        "SDL2 Mixer",
        "SDL2.Mixer",
        "SDL_mixerNative",
        null,
        "spikes/binding-generators/clangsharp/src/Janset.SDL2.Mixer/Generated/Compat",
        "spikes/binding-generators/clangsharp/src/Janset.SDL2.Mixer/Generated/Modern",
        "external/sdl2-cs/src/SDL2_mixer.cs",
        false);

    public static readonly FamilyConfig Sdl2Gfx = new(
        "sdl2-gfx",
        "SDL2 GFX",
        "SDL2.Gfx",
        "SDL2_gfxNative",
        null,
        "spikes/binding-generators/clangsharp/src/Janset.SDL2.Gfx/Generated/Compat",
        "spikes/binding-generators/clangsharp/src/Janset.SDL2.Gfx/Generated/Modern",
        "external/sdl2-cs/src/SDL2_gfx.cs",
        false);
}
