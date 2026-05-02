using static SDL2.SDL;

namespace Sandbox;

/// <summary>
/// Minimal Janset.SDL2 sandbox: opens a 640x480 window, pumps events, exits on
/// quit or the Escape key. Intended as a scratchpad for interactive experiments
/// against the locally packed <c>Janset.SDL2.*</c> feed — not a test, not shipped.
/// </summary>
internal static class Program
{
    private const string WindowTitle = "Janset.SDL2 Sandbox";
    private const int WindowWidth = 640;
    private const int WindowHeight = 480;

    public static int Main()
    {
        if (SDL_Init(SDL_INIT_VIDEO) < 0)
        {
            Console.WriteLine("SDL_Init failed: " + SDL_GetError());
            return 1;
        }

        var window = SDL_CreateWindow(
            WindowTitle,
            SDL_WINDOWPOS_CENTERED,
            SDL_WINDOWPOS_CENTERED,
            WindowWidth,
            WindowHeight,
            SDL_WindowFlags.SDL_WINDOW_SHOWN);

        if (window == IntPtr.Zero)
        {
            Console.WriteLine("SDL_CreateWindow failed: " + SDL_GetError());
            SDL_Quit();
            return 1;
        }

        try
        {
            Console.WriteLine("Sandbox window open. Close the window or press Escape to exit.");
            RunEventLoop();
            return 0;
        }
        finally
        {
            SDL_DestroyWindow(window);
            SDL_Quit();
        }
    }

    private static void RunEventLoop()
    {
        var running = true;
        while (running)
        {
            while (SDL_PollEvent(out var sdlEvent) != 0)
            {
                if (sdlEvent.type != SDL_EventType.SDL_QUIT && sdlEvent is not { type: SDL_EventType.SDL_KEYDOWN, key.keysym.sym: SDL_Keycode.SDLK_ESCAPE })
                {
                    continue;
                }
                running = false;
                break;
            }

            SDL_Delay(16);
        }
    }
}
