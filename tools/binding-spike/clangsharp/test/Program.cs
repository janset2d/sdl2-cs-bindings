// C# port of giroletm/SDL2_gfx/test/testframerate.c
// Exercises our spike-generated SDL2_gfx bindings + sdl2-cs SDL2 core.
//
// Original C uses SDLTest_CommonCreateState harness (separate -lSDL2_test lib, not available
// from C#). C# port replaces with direct SDL_Init/CreateWindow/CreateRenderer.
//
// Native function names match the vcpkg-built SDL2_gfx (Andreas Schiffler upstream) which
// uses prefix-less names (pixelColor, lineRGBA, etc.). The giroletm fork prefixes everything
// with GFX_ but that's a fork-only choice; vcpkg ships the original prefix-less library.

using System;
using System.Runtime.InteropServices;
using SDL2;
using Janset.Spike.SDL2.Gfx;

namespace Janset.Spike.SDL2.Gfx.Test;

internal static class Program
{
    private const int WIDTH = 640;
    private const int HEIGHT = 480;
    private const int TARGET_FPS_INIT = 30;
    private const int TOTAL_FRAMES = 180;  // ~6 seconds at 30fps

    public static int Main()
    {
        // Detect which spike binding side this test app was linked against — assembly name
        // suffix is `.ClangSharp` or `.CppAst`. Shared Program.cs across both test apps via
        // Compile Include link; runtime detection avoids hardcoding either name.
        var asmName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name ?? "(unknown)";
        var generatorSide = asmName.EndsWith(".ClangSharp") ? "ClangSharp" :
                            asmName.EndsWith(".CppAst") ? "CppAst" :
                            "(unknown)";

        Console.WriteLine($"Janset SDL2_gfx spike — {generatorSide} bindings");
        Console.WriteLine($"  Assembly: {asmName}");
        Console.WriteLine($"  SDL_Init...");

        if (SDL.SDL_Init(SDL.SDL_INIT_VIDEO) < 0)
        {
            Console.Error.WriteLine($"SDL_Init failed: {SDL.SDL_GetError()}");
            return 1;
        }

        IntPtr window = IntPtr.Zero;
        IntPtr renderer = IntPtr.Zero;

        try
        {
            window = SDL.SDL_CreateWindow(
                "Janset SDL2_gfx spike — ClangSharp",
                SDL.SDL_WINDOWPOS_CENTERED, SDL.SDL_WINDOWPOS_CENTERED,
                WIDTH, HEIGHT,
                SDL.SDL_WindowFlags.SDL_WINDOW_SHOWN);
            if (window == IntPtr.Zero)
            {
                Console.Error.WriteLine($"SDL_CreateWindow failed: {SDL.SDL_GetError()}");
                return 1;
            }
            Console.WriteLine($"  SDL_CreateWindow OK");

            renderer = SDL.SDL_CreateRenderer(window, -1, SDL.SDL_RendererFlags.SDL_RENDERER_ACCELERATED);
            if (renderer == IntPtr.Zero)
            {
                Console.Error.WriteLine($"SDL_CreateRenderer failed: {SDL.SDL_GetError()}");
                return 1;
            }
            Console.WriteLine($"  SDL_CreateRenderer OK");

            // FPSmanager is a POD struct emitted by both spike generators.
            // ClangSharp output: SDL_initFramerate(FPSmanager* manager) — call with &fpsm
            var fpsm = default(FPSmanager);
            unsafe
            {
                SDL_gfx.SDL_initFramerate(&fpsm);
                SDL_gfx.SDL_setFramerate(&fpsm, TARGET_FPS_INIT);
            }
            Console.WriteLine($"  SDL2_gfx framerate initialized at {TARGET_FPS_INIT} FPS");

            // Bouncing-circle state — direct from testframerate.c
            short x = WIDTH / 2;
            short y = HEIGHT / 2;
            short dx = 3;
            short dy = 2;

            var rng = new Random(42);
            byte r = (byte)rng.Next(256);
            byte g = (byte)rng.Next(256);
            byte b = (byte)rng.Next(256);

            int timeout = 60;
            int totalElapsedMs = 0;

            for (int frame = 0; frame < TOTAL_FRAMES; frame++)
            {
                // Pump events (so window stays responsive + we honor QUIT)
                while (SDL.SDL_PollEvent(out var ev) != 0)
                {
                    if (ev.type == SDL.SDL_EventType.SDL_QUIT)
                    {
                        Console.WriteLine("  Early quit (window closed)");
                        goto cleanup;
                    }
                }

                // Random framerate change every "timeout" frames (mimics testframerate.c logic)
                timeout--;
                if (timeout < 0)
                {
                    int newRate = 5 + 5 * (rng.Next(10));
                    timeout = 2 * newRate;
                    r = (byte)rng.Next(256);
                    g = (byte)rng.Next(256);
                    b = (byte)rng.Next(256);
                    unsafe { SDL_gfx.SDL_setFramerate(&fpsm, (uint)newRate); }
                }

                // Bounce
                x += dx;
                y += dy;
                if (x < 30 || x > WIDTH - 30) dx = (short)-dx;
                if (y < 30 || y > HEIGHT - 30) dy = (short)-dy;

                // Clear to black
                SDL.SDL_SetRenderDrawColor(renderer, 0, 0, 0, 255);
                SDL.SDL_RenderClear(renderer);

                // Primitives from generated SDL2_gfx bindings — ClangSharp output uses [NativeTypeName] but the
                // managed signature is `int filledCircleRGBA(IntPtr renderer, short x, short y, short rad, byte r, byte g, byte b, byte a)`.
                SDL_gfx.filledCircleRGBA(renderer, x, y, 30, r, g, b, 255);
                SDL_gfx.circleRGBA(renderer, x, y, 30, 255, 255, 255, 255);
                SDL_gfx.rectangleRGBA(renderer, 10, 10, 250, 50, 255, 255, 0, 255);

                // String marshalling — ClangSharp emits `sbyte* s` for `const char *`, no string overload.
                // We materialize a UTF-8 buffer manually and pin it.
                var hudText = $"Janset SDL2_gfx spike ({generatorSide}) frame {frame}/{TOTAL_FRAMES}";
                unsafe
                {
                    var bytes = System.Text.Encoding.UTF8.GetBytes(hudText + "\0");
                    fixed (byte* bp = bytes)
                    {
                        SDL_gfx.stringRGBA(renderer, 20, 20, (sbyte*)bp, 255, 255, 255, 255);
                    }
                }

                SDL.SDL_RenderPresent(renderer);

                unsafe
                {
                    var delayMs = SDL_gfx.SDL_framerateDelay(&fpsm);
                    totalElapsedMs += (int)delayMs;
                }
            }

cleanup:
            Console.WriteLine($"  Frames rendered: {TOTAL_FRAMES}");
            Console.WriteLine($"  Total framerate-delay accumulated: {totalElapsedMs}ms");
        }
        finally
        {
            if (renderer != IntPtr.Zero) SDL.SDL_DestroyRenderer(renderer);
            if (window != IntPtr.Zero) SDL.SDL_DestroyWindow(window);
            SDL.SDL_Quit();
            Console.WriteLine("  SDL_Quit. Bye.");
        }

        return 0;
    }
}
