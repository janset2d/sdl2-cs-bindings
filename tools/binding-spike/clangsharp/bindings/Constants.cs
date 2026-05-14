// Manual constants — supplements generated output.
// Single point for macros that ClangSharp emitted as duplicates across multiple
// headers (M_PI was defined in both SDL2_gfxPrimitives.h and SDL2_rotozoom.h,
// causing CS0102 duplicate-member when merged into one partial class).

namespace Janset.Spike.SDL2.Gfx;

public static partial class SDL_gfx
{
    public const double M_PI = 3.1415926535897932384626433832795;
}
