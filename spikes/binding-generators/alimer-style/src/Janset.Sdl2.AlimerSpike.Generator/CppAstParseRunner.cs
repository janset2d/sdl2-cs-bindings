using CppAst;

namespace Janset.Sdl2.AlimerSpike.Generator;

// Mirrors build/_build/Targets/GenerateBindings/Parse/CppAstParseRunner.cs but
// trimmed for spike scope: Windows-local, no synthetic headers, no platform
// parse views, no diagnostic formatter. The parse defines and clang args
// match what manifest.json:189-201 ships for the SDL2.Core family — these
// are the same neutralizers ppy bakes into its base RSP plus SDL_stdinc.rsp.
internal sealed class CppAstParseRunner
{
    public CppCompilation ParseHeader(string headerPath, string includeRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(headerPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(includeRoot);

        var options = new CppParserOptions
        {
            ParseMacros = false,
            ParserKind = CppParserKind.C,
            SystemIncludeFolders = { includeRoot },
        };

        options.Defines.Add("SDL_DECLSPEC=");
        options.Defines.Add("SDL_DISABLE_IMMINTRIN_H=1");
        options.Defines.Add("SDL_DISABLE_MMINTRIN_H=1");
        options.Defines.Add("SDL_DISABLE_XMMINTRIN_H=1");
        options.Defines.Add("SDL_DISABLE_EMMINTRIN_H=1");
        options.Defines.Add("SDL_DISABLE_PMMINTRIN_H=1");
        options.Defines.Add("SDL_DISABLE_MM3DNOW_H=1");
        options.Defines.Add("SDL_DISABLE_LSX_H=1");
        options.Defines.Add("SDL_DISABLE_LASX_H=1");
        options.Defines.Add("SDL_DISABLE_ARM_NEON_H=1");
        options.Defines.Add("SDL_SLOW_MEMCPY");
        options.Defines.Add("SDL_SLOW_MEMMOVE");
        options.Defines.Add("SDL_SLOW_MEMSET");
        options.Defines.Add("SDL_DISABLE_ALLOCA");

        options.AdditionalArguments.Add("-fdeclspec");
        options.AdditionalArguments.Add("-U__has_builtin");

        return CppParser.ParseFile(headerPath, options);
    }
}
