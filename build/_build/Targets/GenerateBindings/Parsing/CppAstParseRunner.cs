using Build.Targets.GenerateBindings.HeaderSet;
using CppAst;

namespace Build.Targets.GenerateBindings.Parsing;

public interface ICppAstParseRunner
{
    CppAstParseResult Parse(ResolvedHeaderSet headerSet, PlatformParseView parseView);
}

public sealed class CppAstParseRunner(ParseDiagnosticFormatter diagnosticFormatter) : ICppAstParseRunner
{
    // SDL_DECLSPEC carries platform-specific export attributes (__declspec(dllexport)
    // on Windows, __attribute__((visibility("default"))) on Linux). Suppress the
    // attribute so CppAst sees plain function declarations.
    //
    // SDL_DISABLE_*_H short-circuits SDL_cpuinfo.h's intrinsic-header includes
    // (immintrin/mmintrin/xmmintrin/emmintrin/pmmintrin/mm3dnow/lsx/lasx/arm_neon).
    // Without these defines, SDL_cpuinfo.h pulls GCC's intrinsic headers (via the
    // Dockerfile's CPATH), whose `extern __inline` declarations of _mm_pause /
    // _mm_getcsr / __rdtsc / _mm_clflush / _mm_{l,m,s}fence collide with libclang's
    // internal builtin-function table and break the parse with
    // `error: definition of builtin function ...`. The disable macros are the
    // documented SDL2 escape hatch for binding generators — see SDL_cpuinfo.h
    // lines 118-133 in any SDL2 release. Peer evidence: amerkoleci's
    // Alimer.Bindings.SDL uses the SDL3 equivalents (SDL_PLATFORM_ANDROID/IOS/WINRT)
    // for the same purpose.
    private readonly IReadOnlyList<string> _baseDefines =
    [
        "SDL_DECLSPEC=",
        "SDL_DISABLE_IMMINTRIN_H=1",
        "SDL_DISABLE_MMINTRIN_H=1",
        "SDL_DISABLE_XMMINTRIN_H=1",
        "SDL_DISABLE_EMMINTRIN_H=1",
        "SDL_DISABLE_PMMINTRIN_H=1",
        "SDL_DISABLE_MM3DNOW_H=1",
        "SDL_DISABLE_LSX_H=1",
        "SDL_DISABLE_LASX_H=1",
        "SDL_DISABLE_ARM_NEON_H=1",
    ];

    // Clang CLI args applied to every parse view, in addition to per-view -U<macro>
    // flags computed from PlatformParseView.Undefines.
    //
    // -U__has_builtin forces SDL_stdinc.h:127-131 `#ifdef __has_builtin` to false,
    // which makes `_SDL_HAS_BUILTIN(x)` always expand to 0. That short-circuits the
    // `#if _SDL_HAS_BUILTIN(__builtin_{mul,add}_overflow)` blocks at SDL_stdinc.h:822
    // and 853, so `_SDL_size_mul_overflow_builtin` and `_SDL_size_add_overflow_builtin`
    // SDL_FORCE_INLINE helpers never enter the AST. Defense-in-depth complement to
    // the translator's CppFunctionFlags.Inline filter (CppAstToPreviewModel.cs);
    // kills 2 of the 16 known SDL_FORCE_INLINE leaks at parse time before the AST
    // filter ever sees them. Peer reference: ppy/SDL3-CS SDL_stdinc.rsp uses the
    // exact same flag.
    //
    // Side-effect audit (2026-05-16): three SDL2 sites reference `_SDL_HAS_BUILTIN`
    // beyond the two SDL_stdinc.h declaration gates above —
    //   SDL_assert.h:54  (selects __builtin_debugtrap for SDL_TriggerBreakpoint
    //                     impl — macro body, no declaration impact),
    //   SDL_endian.h:134/136/138 (selects __builtin_bswap{16,32,64} for SDL_Swap*
    //                     SDL_FORCE_INLINE bodies — body content, helpers are
    //                     already inline-filtered by the AST filter).
    // No public-API declaration is affected by undefining the macro.
    private static readonly IReadOnlyList<string> BaseAdditionalArguments =
    [
        "-U__has_builtin",
    ];

    private readonly ParseDiagnosticFormatter _diagnosticFormatter = diagnosticFormatter ?? throw new ArgumentNullException(nameof(diagnosticFormatter));

    public CppParserOptions CreateOptions(ResolvedHeaderSet headerSet, PlatformParseView parseView)
    {
        ArgumentNullException.ThrowIfNull(headerSet);
        ArgumentNullException.ThrowIfNull(parseView);

        // TargetSystem = "linux" pins libclang to the actual host target (the
        // binding-generator container runs Linux x86_64). CppAst defaults to "windows"
        // even on a Linux host (CppParserOptions ctor); without override, the libclang
        // triple becomes x86_64-pc-windows- and SDL_stdinc.h activates its _MSC_VER
        // branch at line 357 (`#include <sal.h>`), which fails inside our container.
        //
        // SystemIncludeFolders carries only the vcpkg SDL2 header set. Compiler-shipped
        // headers (stddef.h, stdint.h, stdarg.h) and glibc headers (sys/types.h, etc.)
        // are resolved by libclang via the CPATH env var contract in
        // docker/binding-generator.Dockerfile — NuGet libclang.runtime.* does not ship
        // a clang resource directory (verified by package contents; documented by
        // ClangSharp issue #414).
        // SyntheticIncludeRoot must come FIRST so libclang resolves stub stand-ins
        // (process.h, windows.h, etc.) instead of failing-closed on the real Windows/
        // Apple system headers that don't exist inside the Linux container.
        var options = new CppParserOptions
        {
            ParseMacros = true,
            ParserKind = CppParserKind.C,
            TargetSystem = "linux",
            SystemIncludeFolders =
            {
                headerSet.SyntheticIncludeRoot.FullPath,
                headerSet.IncludeRoot.FullPath,
            },
        };

        options.Defines.AddRange(_baseDefines);
        options.Defines.AddRange(parseView.Defines);

        // Enable `__declspec` parsing. Libegl-dev's /usr/include/EGL/egl.h declares
        // its functions with EGLAPI which expands to `__declspec(dllimport/export)`
        // on a Windows target — and SDL_egl.h is pulled by SDL_video.h under
        // SDL_VIDEO_DRIVER_WINDOWS. Without -fdeclspec, clang's default C mode
        // rejects every EGL function declaration in Windows-flavoured views. The
        // flag is narrower than -fms-extensions (which enables the full MS dialect).
        options.AdditionalArguments.Add("-fdeclspec");

        options.AdditionalArguments.AddRange(BaseAdditionalArguments);

        foreach (var undefine in parseView.Undefines)
        {
            options.AdditionalArguments.Add($"-U{undefine}");
        }

        return options;
    }

    // Parses every SDL2 header in its own translation unit and returns the per-header
    // compilations bundled under the parse view. Each ParseFile call is an isolated
    // libclang TU: a parse failure in one header does not poison the others, and
    // transitive include chains stay narrow per header.
    //
    // This is the peer-validated pattern: amerkoleci/Alimer.Bindings.SDL (CppAst, SDL3)
    // and ppy/SDL3-CS (ClangSharp) both loop per header rather than batching headers
    // into one TU. CppAst's CppParser.ParseFile internally delegates to ParseFiles
    // with a singleton list, so the loop — not the API choice — is what isolates.
    public CppAstParseResult Parse(ResolvedHeaderSet headerSet, PlatformParseView parseView)
    {
        ArgumentNullException.ThrowIfNull(headerSet);
        ArgumentNullException.ThrowIfNull(parseView);

        var options = CreateOptions(headerSet, parseView);
        var compilations = new List<CppCompilation>(headerSet.Headers.Count);

        foreach (var header in headerSet.Headers)
        {
            var compilation = CppParser.ParseFile(header.FullPath, options);
            if (compilation.HasErrors)
            {
                throw new InvalidOperationException(_diagnosticFormatter.FormatErrors($"{parseView.Name} [{header.GetFilename()}]", compilation));
            }

            compilations.Add(compilation);
        }

        return new CppAstParseResult(parseView, compilations);
    }
}
