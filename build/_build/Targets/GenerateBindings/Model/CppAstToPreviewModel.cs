using Build.Targets.GenerateBindings.Parsing;
using CppAst;

namespace Build.Targets.GenerateBindings.Model;

internal static class CppAstToPreviewModel
{
    public static PreviewBindingModel Translate(
        IReadOnlyList<CppAstParseResult> parseResults,
        IReadOnlySet<string> excludedFunctionNames,
        IReadOnlyList<PreviewFunction> requiredFunctions)
    {
        ArgumentNullException.ThrowIfNull(parseResults);
        ArgumentNullException.ThrowIfNull(excludedFunctionNames);
        ArgumentNullException.ThrowIfNull(requiredFunctions);

        var neutralFunctions = ExtractNeutralFunctionNames(parseResults, excludedFunctionNames);

        // RequiredFunctions are hand-curated declarations for public symbols the
        // per-header parse loop cannot reach (e.g. SDL.h-only base functions like
        // SDL_Init that survive only in the excluded umbrella header). They extend
        // the Neutral view's symbol set so platform-view dedup correctly subtracts
        // them from per-OS view emits.
        foreach (var required in requiredFunctions)
        {
            neutralFunctions.Add(required.Name);
        }

        var views = new List<PreviewParseView>(parseResults.Count);

        foreach (var result in parseResults)
        {
            var isNeutral = string.Equals(result.ParseView.Name, "Neutral", StringComparison.Ordinal);
            var functions = ExtractFunctions(result.Compilations, excludedFunctionNames);

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

            views.Add(new PreviewParseView(
                Name: result.ParseView.Name,
                SupportedOsPlatform: result.ParseView.SupportedOsPlatform,
                Functions: functions));
        }

        return new PreviewBindingModel(views);
    }

    private static HashSet<string> ExtractNeutralFunctionNames(
        IReadOnlyList<CppAstParseResult> parseResults,
        IReadOnlySet<string> excludedFunctionNames)
    {
        var neutral = parseResults.FirstOrDefault(r => string.Equals(r.ParseView.Name, "Neutral", StringComparison.Ordinal));
        if (neutral is null)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        return ExtractFunctions(neutral.Compilations, excludedFunctionNames)
            .Select(f => f.Name)
            .ToHashSet(StringComparer.Ordinal);
    }

    // Per-header parsing produces one CppCompilation per SDL2 header. A function
    // declared in SDL_video.h appears in that header's compilation directly AND in
    // any other header's compilation that transitively includes SDL_video.h. Both
    // surface the same f.SourceFile (the real declaration site), so deduping by
    // (SourceFile, Name) collapses the umbrella-include duplicates without losing
    // any unique declarations. C has no overloads, so (SourceFile, Name) is unique.
    private static List<PreviewFunction> ExtractFunctions(
        IReadOnlyList<CppCompilation> compilations,
        IReadOnlySet<string> excludedFunctionNames)
    {
        var seen = new HashSet<(string SourceFile, string Name)>();
        var functions = new List<PreviewFunction>();

        foreach (var compilation in compilations)
        {
            foreach (var function in compilation.Functions)
            {
                if (!IsBindableSdl2Export(function, excludedFunctionNames))
                {
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
    private static bool IsBindableSdl2Export(CppFunction function, IReadOnlySet<string> excludedFunctionNames)
    {
        return IsSdl2Header(function.SourceFile)
            && !string.IsNullOrWhiteSpace(function.Name)
            && !function.Flags.HasFlag(CppFunctionFlags.Inline)
            && !excludedFunctionNames.Contains(function.Name);
    }

    private static PreviewFunction ToFunction(CppFunction function)
    {
        var sourceHeader = Path.GetFileName(function.SourceFile ?? string.Empty);
        var parameters = function.Parameters
            .Select(p => new PreviewParameter(MapType(p.Type), SafeIdentifier(p.Name)))
            .ToList();

        return new PreviewFunction(
            Name: function.Name,
            ReturnType: MapType(function.ReturnType),
            Parameters: parameters,
            SourceHeader: sourceHeader);
    }

    private static string SafeIdentifier(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return "@_";
        }
        return name switch
        {
            "ref" or "out" or "in" or "params" or "object" or "string" or "event" => "@" + name,
            _ => name,
        };
    }

    internal static string MapType(CppType type)
    {
        while (type is CppQualifiedType qt)
        {
            type = qt.ElementType;
        }

        return type switch
        {
            CppPrimitiveType prim => MapPrimitive(prim),
            CppPointerType ptr => MapPointer(ptr),
            CppTypedef td => MapTypedef(td),
            CppArrayType arr => MapPointer(new CppPointerType(arr.ElementType)),
            CppEnum => "int",
            CppClass cls => cls.Name,
            _ => "IntPtr",
        };
    }

    // C `long` is platform-sized: 64-bit on LP64 (Linux/macOS) and 32-bit on
    // LLP64 (Windows). We parse Linux headers; emitting `int` for Long would
    // truncate to 32 bits on the LP64 runtime ABI. `nint`/`nuint` round-trip
    // correctly on both target families because the CLR resolves them to the
    // pointer-sized integer of the host process.
    internal static string MapPrimitive(CppPrimitiveType prim) => prim.Kind switch
    {
        CppPrimitiveKind.Void => "void",
        CppPrimitiveKind.Bool => "byte",
        CppPrimitiveKind.Char => "sbyte",
        CppPrimitiveKind.WChar => "char",
        CppPrimitiveKind.Short => "short",
        CppPrimitiveKind.Int => "int",
        CppPrimitiveKind.LongLong => "long",
        CppPrimitiveKind.UnsignedChar => "byte",
        CppPrimitiveKind.UnsignedShort => "ushort",
        CppPrimitiveKind.UnsignedInt => "uint",
        CppPrimitiveKind.UnsignedLongLong => "ulong",
        CppPrimitiveKind.Float => "float",
        CppPrimitiveKind.Double => "double",
        CppPrimitiveKind.Long => "nint",
        CppPrimitiveKind.UnsignedLong => "nuint",
        _ => "IntPtr",
    };

    internal static string MapPointer(CppPointerType ptr)
    {
        var element = ptr.ElementType;
        while (element is CppQualifiedType qt)
        {
            element = qt.ElementType;
        }

        return element switch
        {
            CppPrimitiveType { Kind: CppPrimitiveKind.Void } => "IntPtr",
            CppPrimitiveType prim => MapPrimitive(prim) + "*",
            CppTypedef td when td.Name.StartsWith("SDL_", StringComparison.Ordinal) => "IntPtr",
            CppTypedef td => MapTypedefPointer(td),
            CppClass cls when cls.Name.StartsWith("ID", StringComparison.Ordinal) => "IntPtr",
            CppClass cls when cls.Name.StartsWith("SDL_", StringComparison.Ordinal) => "IntPtr",
            CppClass cls => cls.Name + "*",
            _ => "IntPtr",
        };
    }

    internal static string MapTypedefPointer(CppTypedef td)
    {
        var element = td.ElementType;
        while (element is CppQualifiedType qt)
        {
            element = qt.ElementType;
        }

        if (element is CppPrimitiveType prim)
        {
            return MapPrimitive(prim) + "*";
        }
        if (element is CppTypedef nested)
        {
            return MapTypedefPointer(nested);
        }
        return td.Name + "*";
    }

    // Order matters: explicit-width SDL2 typedefs (Sint8…Uint64, SDL_bool,
    // size_t/ptrdiff_t) hit the explicit table first; everything else
    // chain-resolves through the typedef target so SDL_*-prefixed primitive
    // aliases (SDL_AudioFormat→Uint16, SDL_SpinLock→int, SDL_GameControllerButton→
    // enum) emit the underlying primitive width. SDL_*-prefixed opaque struct
    // typedefs (SDL_Window, SDL_Renderer, etc.) reach the final IntPtr fallback
    // because their target is a CppClass with no primitive resolution path.
    internal static string MapTypedef(CppTypedef td)
    {
        switch (td.Name)
        {
            case "Sint8": return "sbyte";
            case "Uint8": return "byte";
            case "Sint16": return "short";
            case "Uint16": return "ushort";
            case "Sint32": return "int";
            case "Uint32": return "uint";
            case "Sint64": return "long";
            case "Uint64": return "ulong";
            case "SDL_bool": return "byte";
            case "size_t": return "nuint";
            case "ptrdiff_t": return "nint";
        }

        var element = td.ElementType;
        while (element is CppQualifiedType qt)
        {
            element = qt.ElementType;
        }

        switch (element)
        {
            case CppPrimitiveType prim:
                return MapPrimitive(prim);
            case CppTypedef nested:
                return MapTypedef(nested);
            case CppEnum:
                return "int";
        }

        if (td.Name.StartsWith("SDL_", StringComparison.Ordinal))
        {
            return "IntPtr";
        }

        return MapType(td.ElementType);
    }
}
