using Cake.Common.IO;
using Cake.Core;
using Cake.Core.IO;

namespace Build.Targets.GenerateBindings.HeaderSet;

// Public because GenerateBindingsTask (sibling Cake Frosting Task convention is
// `public sealed class XxxTask`) takes HeaderSetResolver in its ctor; CS0051
// blocks the internal flip until the wider Task-visibility convention shifts.
public sealed class HeaderSetResolver(ICakeContext context)
{
    private const string Sdl2HeaderGlob = "*.h";
    private readonly ICakeContext _context = context ?? throw new ArgumentNullException(nameof(context));

    // Stage 1 scope is SDL2.Core. The vcpkg hybrid triplet installs satellite
    // libraries (SDL2_image, SDL2_mixer, SDL2_ttf, SDL2_net, SDL2_gfx) under the
    // same SDL2/ include root, so the *.h glob also picks up satellite headers
    // that declare IMG_* / Mix_* / TTF_* / SDLNet_* / SDL2_* surface. Those
    // belong to future satellite slices, not SDL2.Core.
    //
    // SDL.h is the umbrella that #includes every other SDL2 header. Under per-header
    // parsing (CppAstParseRunner.Parse loop) it would expand into one giant translation
    // unit that defeats the isolation, so it is excluded here.
    //
    // SDL.h is also a declarator in its own right: it carries 5 base API functions
    // (SDL_Init, SDL_InitSubSystem, SDL_QuitSubSystem, SDL_WasInit, SDL_Quit) that
    // exist nowhere else in the SDL2 source tree. Excluding SDL.h drops them from
    // the AST. The recovery path is the consumer-facing config's RequiredFunctions
    // fallback list (Sdl2CoreGenerationConfig) — hand-curated P/Invoke declarations
    // merged into the Neutral view by the translator. The earlier draft of this
    // comment claimed "every symbol [SDL.h] would surface is also declared in one
    // of the headers it includes"; that was wrong and is retracted.
    //
    // begin_code.h / close_code.h are pragma-pack scaffolding pseudo-headers paired
    // around SDL function declarations. They contain no bindable AST surface — only
    // pragma directives and SDL_ macros — and close_code.h fails to parse standalone
    // (it #errors out without begin_code.h's macro state).
    //
    // SDL_opengl.h / SDL_opengles.h / SDL_opengles2.h / SDL_egl.h declare ZERO
    // SDL_* functions (grep extern DECLSPEC == 0). They are convenience wrappers
    // that #include the host's GL / EGL / GLES headers so consumers can call raw
    // graphics-API entry points alongside SDL. Excluding them is harmless to the
    // binding model AND avoids dragging in platform-specific GL chains
    // (Apple OpenGLES/ES1/gl.h, Windows windows.h, etc.). SDL_vulkan.h and
    // SDL_metal.h, by contrast, declare SDL_Vulkan_* / SDL_Metal_* functions and
    // are kept.
    //
    // SDL_opengl_glext.h + SDL_opengles2_* are sub-headers that depend on type
    // setup performed by their umbrella. With the umbrella excluded, the
    // sub-headers are even less reachable — they remain on the exclusion list
    // defensively in case the glob picks them up.
    //
    // SDL_test*.h declares the SDLTest_* test-harness helpers (SDL2's internal test
    // suite scaffolding). Not part of the public binding surface; SDL2-CS does not
    // bind them.
    private static readonly HashSet<string> ExcludedHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        // Umbrella / scaffolding
        "SDL.h",
        "begin_code.h",
        "close_code.h",

        // Satellite library umbrellas (not SDL2.Core surface)
        "SDL_image.h",
        "SDL_mixer.h",
        "SDL_net.h",
        "SDL_ttf.h",

        // Graphics-API convenience wrappers (zero SDL_ functions)
        "SDL_opengl.h",
        "SDL_opengles.h",
        "SDL_opengles2.h",
        "SDL_egl.h",

        // OpenGL / GLES sub-headers (require umbrella's type setup)
        "SDL_opengl_glext.h",
        "SDL_opengles2_gl2.h",
        "SDL_opengles2_gl2ext.h",
        "SDL_opengles2_gl2platform.h",
        "SDL_opengles2_khrplatform.h",
    };

    public ResolvedHeaderSet ResolveSdl2CoreHeaders(DirectoryPath vcpkgInstalledDirectory, DirectoryPath syntheticHeadersDirectory, string triplet)
    {
        ArgumentNullException.ThrowIfNull(vcpkgInstalledDirectory);
        ArgumentNullException.ThrowIfNull(syntheticHeadersDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(triplet);

        var includeRoot = vcpkgInstalledDirectory.Combine(triplet).Combine("include");
        var sdl2Include = includeRoot.Combine("SDL2");

        if (!_context.DirectoryExists(sdl2Include))
        {
            throw new CakeException($"SDL2 include directory was not found at '{sdl2Include.FullPath}'.");
        }

        var headers = _context.GetFiles($"{sdl2Include.FullPath}/{Sdl2HeaderGlob}")
            .Where(path => !IsExcluded(path.GetFilename().FullPath))
            .OrderBy(path => path.FullPath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return headers.Count != 0
            ? new ResolvedHeaderSet(includeRoot, syntheticHeadersDirectory, sdl2Include, headers)
            : throw new CakeException($"No SDL2 headers matching '{Sdl2HeaderGlob}' were found at '{sdl2Include.FullPath}'.");
    }

    private static bool IsExcluded(string fileName)
    {
        return ExcludedHeaders.Contains(fileName)
               || fileName.StartsWith("SDL_test", StringComparison.OrdinalIgnoreCase)
               // SDL2_framerate.h, SDL2_gfxPrimitives.h, SDL2_rotozoom.h, etc. — SDL2_gfx satellite.
               || fileName.StartsWith("SDL2_", StringComparison.OrdinalIgnoreCase);
    }
}
