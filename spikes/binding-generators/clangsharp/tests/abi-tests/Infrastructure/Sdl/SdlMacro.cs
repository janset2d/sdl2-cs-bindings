using SDL2;
using static SDL2.SDLNative;

namespace Janset.SDL2.AbiTests.Infrastructure.Sdl;

internal static unsafe class SdlMacro
{
    public static SDL_Surface* LoadBmp(string path)
    {
        using PinnedUtf8 pinnedPath = SdlUtf8.Pin(path);
        using PinnedUtf8 readMode = SdlUtf8.Pin("rb");

        SDL_RWops source = SDL_RWFromFile(pinnedPath.Pointer, readMode.Pointer);
        if (source.IsNull)
        {
            throw new InvalidOperationException($"SDL_RWFromFile failed for BMP load: {SdlError.Current}");
        }

        // Mirrors SDL_LoadBMP(file): SDL_LoadBMP_RW(SDL_RWFromFile(file, "rb"), 1).
        SDL_Surface* surface = SDL_LoadBMP_RW(source, 1);
        if (surface is null)
        {
            throw new InvalidOperationException($"SDL_LoadBMP_RW failed: {SdlError.Current}");
        }

        return surface;
    }

    public static int SaveBmp(SDL_Surface* surface, string path)
    {
        using PinnedUtf8 pinnedPath = SdlUtf8.Pin(path);
        using PinnedUtf8 writeMode = SdlUtf8.Pin("wb");

        SDL_RWops destination = SDL_RWFromFile(pinnedPath.Pointer, writeMode.Pointer);
        if (destination.IsNull)
        {
            throw new InvalidOperationException($"SDL_RWFromFile failed for BMP save: {SdlError.Current}");
        }

        // Mirrors SDL_SaveBMP(surface, file): SDL_SaveBMP_RW(surface, SDL_RWFromFile(file, "wb"), 1).
        return SDL_SaveBMP_RW(surface, destination, 1);
    }
}
