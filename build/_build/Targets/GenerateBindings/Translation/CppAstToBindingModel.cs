using System.Collections.Immutable;
using Build.Data.BindingGeneration.Models;
using Build.Targets.GenerateBindings.Model;
using Build.Targets.GenerateBindings.Parsing;
using CppAst;

namespace Build.Targets.GenerateBindings.Translation;

/// <summary>
/// CppAst parse-result merge step downstream of the per-view PLINQ parsing loop.
/// Phase 3C extraction: type mapping moved out into <see cref="TypeMappingPolicy"/>
/// (static — Phase 3D wires per-family <see cref="CoreOwnedTypeMap"/> when handle
/// classification needs the qualified-reference path), unsupported-declaration
/// filtering moved out into <see cref="KnownUnsupportedDeclarationPolicy"/>.
/// This class focuses on the view-level orchestration (Neutral merge, per-platform
/// dedup, required-function injection). Phase 3D rewrites the translator further
/// to populate the new declaration categories (<see cref="BindingStruct"/> /
/// <see cref="BindingEnumeration"/> / <see cref="BindingConstant"/> /
/// <see cref="BindingHandle"/> / <see cref="BindingCallback"/>) by structurally
/// inspecting the CppAst AST.
/// </summary>
internal static class CppAstToBindingModel
{
    public static BindingModel Translate(
        IReadOnlyList<CppAstParseResult> parseResults,
        BindingGenerationConfig config,
        IReadOnlyList<BindingFunction> requiredFunctions)
    {
        ArgumentNullException.ThrowIfNull(parseResults);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(requiredFunctions);

        var unsupportedPolicy = new KnownUnsupportedDeclarationPolicy(config);

        var neutralFunctions = ExtractNeutralFunctionNames(parseResults, config, unsupportedPolicy);

        // RequiredFunctions are hand-curated declarations for public symbols the
        // per-header parse loop cannot reach (e.g. SDL.h-only base functions like
        // SDL_Init that survive only in the excluded umbrella header). They extend
        // the Neutral view's symbol set so platform-view dedup correctly subtracts
        // them from per-OS view emits.
        foreach (var required in requiredFunctions)
        {
            neutralFunctions.Add(required.Name);
        }

        var views = new List<BindingParseView>(parseResults.Count);

        foreach (var result in parseResults)
        {
            var isNeutral = string.Equals(result.ParseView.Name, "Neutral", StringComparison.Ordinal);
            var functions = ExtractFunctions(result.Compilations, config, unsupportedPolicy);

            if (isNeutral)
            {
                // RequiredFunctions render first in the Neutral emit (mirrors SDL2-CS's
                // visual convention of placing SDL_Init/SDL_Quit at the top of the file).
                functions = [.. requiredFunctions, .. functions];
            }
            else
            {
                // Platform views drop functions that already appeared in Neutral
                // (parsed or required), so SDL_Init isn't duplicated per-OS.
                functions = functions
                    .Where(f => !neutralFunctions.Contains(f.Name))
                    .ToList();
            }

            views.Add(new BindingParseView(
                Name: result.ParseView.Name,
                SupportedOsPlatform: result.ParseView.SupportedOsPlatform,
                Functions: functions));
        }

        return new BindingModel(views);
    }

    private static HashSet<string> ExtractNeutralFunctionNames(
        IReadOnlyList<CppAstParseResult> parseResults,
        BindingGenerationConfig config,
        KnownUnsupportedDeclarationPolicy unsupportedPolicy)
    {
        var neutral = parseResults.FirstOrDefault(r => string.Equals(r.ParseView.Name, "Neutral", StringComparison.Ordinal));
        if (neutral is null)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        return ExtractFunctions(neutral.Compilations, config, unsupportedPolicy)
            .Select(f => f.Name)
            .ToHashSet(StringComparer.Ordinal);
    }

    // Per-header parsing produces one CppCompilation per SDL2 header. A function
    // declared in SDL_video.h appears in that header's compilation directly AND in
    // any other header's compilation that transitively includes SDL_video.h. Both
    // surface the same f.SourceFile (the real declaration site), so deduping by
    // (SourceFile, Name) collapses the umbrella-include duplicates without losing
    // any unique declarations. C has no overloads, so (SourceFile, Name) is unique.
    private static List<BindingFunction> ExtractFunctions(
        IReadOnlyList<CppCompilation> compilations,
        BindingGenerationConfig config,
        KnownUnsupportedDeclarationPolicy unsupportedPolicy)
    {
        var seen = new HashSet<(string SourceFile, string Name)>();
        var functions = new List<BindingFunction>();

        foreach (var compilation in compilations)
        {
            foreach (var function in compilation.Functions)
            {
                if (!IsBindableSdl2Export(function, config.ExcludedFunctions))
                {
                    continue;
                }

                if (unsupportedPolicy.IsUnsupported(function, out _))
                {
                    // Manifest-deferred functions skipped silently here. C-variadic
                    // functions are NOT filtered — they pass through with the fmt-only
                    // shape per SDL2-CS / Alimer peer pattern; Phase 3F adds the
                    // string-fmtAndArglist friendly wrapper that makes the pre-format
                    // expectation explicit at the API surface.
                    continue;
                }

                var key = (function.SourceFile ?? string.Empty, function.Name);
                if (!seen.Add(key))
                {
                    continue;
                }

                functions.Add(ToFunction(function));
            }
        }

        return functions
            .OrderBy(f => f.SourceHeader, StringComparer.Ordinal)
            .ThenBy(f => f.Name, StringComparer.Ordinal)
            .ToList();
    }

    private static bool IsSdl2Header(string? sourceFile)
    {
        if (string.IsNullOrWhiteSpace(sourceFile))
        {
            return false;
        }

        var normalized = sourceFile.Replace('\\', '/');
        return normalized.Contains("/SDL2/", StringComparison.OrdinalIgnoreCase);
    }

    // Public binding surface = SDL2 source-file + non-empty name + NOT inline +
    // NOT explicitly excluded. SDL_FORCE_INLINE helpers (SDL_RectEmpty, SDL_memset4,
    // _SDL_size_*_overflow_builtin, etc.) are declared in SDL2 headers but NOT
    // exported by libSDL2-2.0.so — calling them via P/Invoke would raise
    // EntryPointNotFoundException at runtime. The CppAst Inline flag mirrors
    // libclang's clang_Cursor_isFunctionInlined predicate; peer Silk.NET hard-skips
    // it at the same point in their scrape. The excludedFunctionNames set catches
    // declared-in-header-but-application-defined symbols like SDL_main where the
    // runtime SDL2 library never exports the entry point.
    private static bool IsBindableSdl2Export(CppFunction function, ImmutableHashSet<string> excludedFunctionNames)
    {
        return IsSdl2Header(function.SourceFile)
            && !string.IsNullOrWhiteSpace(function.Name)
            && !function.Flags.HasFlag(CppFunctionFlags.Inline)
            && !excludedFunctionNames.Contains(function.Name);
    }

    private static BindingFunction ToFunction(CppFunction function)
    {
        var sourceHeader = Path.GetFileName(function.SourceFile ?? string.Empty);
        var parameters = function.Parameters
            .Select((p, index) => new BindingParameter(
                TypeMappingPolicy.Map(p.Type),
                TypeMappingPolicy.SafeIdentifier(p.Name, index)))
            .ToList();

        return new BindingFunction(
            Name: function.Name,
            ReturnType: TypeMappingPolicy.Map(function.ReturnType),
            Parameters: parameters,
            SourceHeader: sourceHeader);
    }
}
