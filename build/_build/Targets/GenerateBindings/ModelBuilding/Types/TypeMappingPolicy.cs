using System.Collections.Frozen;
using Build.Data.BindingGeneration.Models;
using Build.Targets.GenerateBindings.Model;
using Build.Targets.GenerateBindings.ModelBuilding.SdlPolicy;
using CppAst;

namespace Build.Targets.GenerateBindings.ModelBuilding.Types;

/// <summary>
/// Legacy CppAst type → managed <see cref="BindingTypeRef"/> projection. The
/// semantic pipeline uses <see cref="NativeTypeClassifier"/> for full type
/// classification; this policy remains as the narrow source of primitive-width,
/// explicit SDL typedef, and safe-identifier rules.
/// <para>
/// <b>Does NOT classify pointers as opaque handles vs struct pointers.</b>
/// Production semantic translators rely on <see cref="NativeTypeClassifier"/> for
/// that decision. The older <see cref="MapPointer"/> fallback remains for legacy
/// tests and adapters that still consume <see cref="BindingTypeRef"/>.
/// </para>
/// <para>
/// Preserved invariants: chain-resolve typedef before SDL-prefix fallback,
/// C <c>long</c> uses platform-sensitive C integer types, catch-all maps to
/// <c>IntPtr</c> rather than silently dropping, typedef recursion is capped
/// by <see cref="MaxTypedefDepth"/>, and <see cref="SafeIdentifier"/> covers
/// C# reserved/contextual keywords.
/// </para>
/// </summary>
public static class TypeMappingPolicy
{
    /// <summary>
    /// Hard cap on typedef chain resolution. SDL2's deepest legitimate chain
    /// today is 3–4 hops (e.g. <c>SDL_AudioFormat</c> → <c>Uint16</c> →
    /// <c>unsigned short</c>). 16 is generous; circular typedefs hit it long
    /// before any legitimate chain.
    /// </summary>
    public const int MaxTypedefDepth = 16;

    /// <summary>
    /// Looks up a typedef name in the explicit-width / special-value map
    /// (e.g. <c>SDL_bool</c> → <c>"int"</c>, <c>Sint8</c> → <c>"sbyte"</c>).
    /// Returns the managed name via <paramref name="managedName"/> when found.
    /// </summary>
    public static bool TryMapExplicitTypedef(string name, out string managedName) =>
        ExplicitTypedefMap.TryGetValue(name, out managedName!);

    private static readonly FrozenDictionary<string, string> ExplicitTypedefMap =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Sint8"] = "sbyte",
            ["Uint8"] = "byte",
            ["Sint16"] = "short",
            ["Uint16"] = "ushort",
            ["Sint32"] = "int",
            ["Uint32"] = "uint",
            ["Sint64"] = "long",
            ["Uint64"] = "ulong",
            ["SDL_bool"] = "int",
            ["size_t"] = "nuint",
            ["ptrdiff_t"] = "nint",
        }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>
    /// C# 12 reserved + contextual keywords that may collide with C parameter
    /// names. Peer-validated idiomatic binding-generator pattern: three of the
    /// dominant .NET binding generators emit the same hardcoded-set escape
    /// (<c>dotnet/ClangSharp.PInvokeGenerator.EscapeName</c> uses a switch case,
    /// <c>amerkoleci/Alimer.Bindings.SDL</c> uses a static <c>HashSet</c>,
    /// <c>dotnet/Silk.NET.BuildTools.AtEscape</c> uses a 77-entry HashSet).
    /// Microsoft.CodeAnalysis.CSharp's <c>SyntaxFacts.GetKeywordKind</c> is the
    /// authoritative alternative but introduces a 22-MB-compressed package
    /// dependency that's not justified for a single-lookup escape path.
    /// <para>
    /// Maintenance: add new contextual keywords as C# language updates introduce
    /// them (<c>file</c>, <c>scoped</c>, <c>required</c> are the recent ones).
    /// Updates are rare (1–2 per .NET release) and the test suite enumerates
    /// the current set, so drift surfaces immediately.
    /// </para>
    /// </summary>
    private static readonly FrozenSet<string> ReservedKeywords =
        new HashSet<string>(StringComparer.Ordinal)
        {
            // Reserved keywords (C# 1.0+)
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char",
            "checked", "class", "const", "continue", "decimal", "default", "delegate",
            "do", "double", "else", "enum", "event", "explicit", "extern", "false",
            "finally", "fixed", "float", "for", "foreach", "goto", "if", "implicit",
            "in", "int", "interface", "internal", "is", "lock", "long", "namespace",
            "new", "null", "object", "operator", "out", "override", "params", "private",
            "protected", "public", "readonly", "ref", "return", "sbyte", "sealed",
            "short", "sizeof", "stackalloc", "static", "string", "struct", "switch",
            "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked",
            "unsafe", "ushort", "using", "virtual", "void", "volatile", "while",
            // Contextual keywords likely to collide with C identifiers
            "add", "alias", "and", "args", "ascending", "async", "await", "by",
            "descending", "dynamic", "equals", "file", "from", "get", "global", "group",
            "init", "into", "join", "let", "managed", "nameof", "nint", "not", "notnull",
            "nuint", "on", "or", "orderby", "partial", "record", "remove", "required",
            "scoped", "select", "set", "unmanaged", "value", "var", "when", "where",
            "with", "yield",
        }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    /// Top-level dispatch for any <see cref="CppType"/>. Unwraps
    /// <see cref="CppQualifiedType"/> (const/volatile qualifiers carry no
    /// managed-side meaning at the binding surface) before dispatching by
    /// concrete shape.
    /// </summary>
    public static BindingTypeRef Map(CppType type)
    {
        ArgumentNullException.ThrowIfNull(type);
        while (type is CppQualifiedType qt)
        {
            type = qt.ElementType;
        }
        return type switch
        {
            CppPrimitiveType prim => MapPrimitive(prim),
            CppPointerType pointer => MapPointer(pointer),
            CppTypedef td when SdlNativeTypeSubstitutionPolicy.TryMap(td, out var mapped) => mapped,
            CppTypedef td when ExternalNativeTypePolicy.TryMapTypedef(td, out var mapped) => mapped,
            CppTypedef td => MapTypedef(td),
            CppArrayType arr => MapPointer(new CppPointerType(arr.ElementType)),
            CppEnum => BindingTypeRef.Of("int"),
            CppClass cls when SdlNativeTypeSubstitutionPolicy.TryMap(cls, out var mapped) => mapped,
            CppClass cls => BindingTypeRef.Of(cls.Name),
            _ => BindingTypeRef.Of("IntPtr"),
        };
    }

    /// <summary>
    /// C primitive → managed primitive. C <c>long</c> and
    /// <c>unsigned long</c> are platform-sensitive ABI integers, so they use
    /// <c>CLong</c> / <c>CULong</c> instead of pointer-sized integer aliases.
    /// </summary>
    public static BindingTypeRef MapPrimitive(CppPrimitiveType prim)
    {
        ArgumentNullException.ThrowIfNull(prim);
        var managedName = prim.Kind switch
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
            CppPrimitiveKind.Long => "CLong",
            CppPrimitiveKind.UnsignedLong => "CULong",
            _ => "IntPtr",
        };
        return BindingTypeRef.Of(managedName);
    }

    /// <summary>
    /// Pointer mapping for legacy callers that still consume <see cref="BindingTypeRef"/>.
    /// SDL_-prefixed typedef and class pointees emit as <c>IntPtr</c>; non-SDL
    /// classes get <c>cls.Name + "*"</c>. The semantic translators use
    /// <see cref="NativeTypeClassifier"/> when they need structural inspection.
    /// </summary>
    public static BindingTypeRef MapPointer(CppPointerType node)
    {
        ArgumentNullException.ThrowIfNull(node);

        var element = node.ElementType;
        while (element is CppQualifiedType qt)
        {
            element = qt.ElementType;
        }

        if (element is CppTypedef typedef && ExternalNativeTypePolicy.TryMapTypedefPointer(typedef, out var mapped))
        {
            return mapped;
        }

        var managedName = element switch
        {
            CppPrimitiveType { Kind: CppPrimitiveKind.Void } => "IntPtr",
            CppPrimitiveType prim => MapPrimitive(prim).ManagedName + "*",
            CppTypedef td when td.Name.StartsWith("SDL_", StringComparison.Ordinal) => "IntPtr",
            CppTypedef td => MapTypedefPointer(td),
            CppClass cls when cls.Name.StartsWith("ID", StringComparison.Ordinal) => "IntPtr",
            CppClass cls when cls.Name.StartsWith("SDL_", StringComparison.Ordinal) => "IntPtr",
            CppClass cls when ExternalNativeTypePolicy.TryMapClassPointer(cls, out var classPointerMapping) => classPointerMapping.ManagedName,
            CppClass cls => cls.Name + "*",
            _ => "IntPtr",
        };
        return BindingTypeRef.Of(managedName);
    }

    /// <summary>
    /// Typedef resolution. Order matters: explicit-width SDL2 typedefs
    /// (<c>Sint8</c>…<c>Uint64</c>, <c>SDL_bool</c>, <c>size_t</c>,
    /// <c>ptrdiff_t</c>) hit the explicit table first; everything else
    /// chain-resolves through the typedef target so SDL_-prefixed primitive
    /// aliases (<c>SDL_AudioFormat</c> → <c>Uint16</c>, <c>SDL_SpinLock</c> →
    /// <c>int</c>, <c>SDL_GameControllerButton</c> → enum) emit the underlying
    /// primitive width rather than falling back to <c>IntPtr</c>.
    /// SDL_-prefixed opaque struct typedefs (<c>SDL_Window</c>, <c>SDL_Renderer</c>)
    /// reach the final <c>IntPtr</c> fallback because their target is a
    /// <see cref="CppClass"/> with no primitive resolution path.
    /// </summary>
    public static BindingTypeRef MapTypedef(CppTypedef td) => MapTypedef(td, depth: 0);

    private static BindingTypeRef MapTypedef(CppTypedef td, int depth)
    {
        if (depth > MaxTypedefDepth)
        {
            throw new InvalidOperationException(
                $"Typedef chain depth exceeded {MaxTypedefDepth} resolving '{td.Name}' — " +
                "possible circular typedef in the SDL2 header set. Inspect the typedef chain " +
                "in vcpkg_installed/x64-linux-hybrid/include/SDL2/ and either break the cycle " +
                "upstream or add the typedef to manifest binding_generation.deferred_declarations.");
        }

        if (ExplicitTypedefMap.TryGetValue(td.Name, out var explicitMapping))
        {
            return BindingTypeRef.Of(explicitMapping);
        }

        if (SdlNativeTypeSubstitutionPolicy.TryMap(td, out var substitutedMapping))
        {
            return substitutedMapping;
        }

        if (ExternalNativeTypePolicy.TryMapTypedef(td, out var externalMapping))
        {
            return externalMapping;
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
                return MapTypedef(nested, depth + 1);
            case CppEnum:
                return BindingTypeRef.Of("int");
        }

        if (td.Name.StartsWith("SDL_", StringComparison.Ordinal))
        {
            return BindingTypeRef.Of("IntPtr");
        }

        return Map(td.ElementType);
    }

    private static string MapTypedefPointer(CppTypedef td) => MapTypedefPointer(td, depth: 0);

    private static string MapTypedefPointer(CppTypedef td, int depth)
    {
        if (depth > MaxTypedefDepth)
        {
            throw new InvalidOperationException(
                $"Typedef pointer chain depth exceeded {MaxTypedefDepth} resolving '{td.Name}*'.");
        }

        if (ExternalNativeTypePolicy.TryMapTypedefPointer(td, out var externalMapping))
        {
            return externalMapping.ManagedName;
        }

        var element = td.ElementType;
        while (element is CppQualifiedType qt)
        {
            element = qt.ElementType;
        }

        if (element is CppPrimitiveType prim)
        {
            return MapPrimitive(prim).ManagedName + "*";
        }
        if (element is CppTypedef nested)
        {
            return MapTypedefPointer(nested, depth + 1);
        }
        return td.Name + "*";
    }

    /// <summary>
    /// Escapes C parameter / identifier names that collide with C# keywords by
    /// prefixing with <c>@</c>. Empty input maps to an indexed fallback when a
    /// parameter index is supplied, or <c>"@_"</c> for non-parameter callers.
    /// CppAst's parameter parser produces empty names for unnamed parameters in
    /// some clang versions. P2.9 — covers the full C# 12 reserved + contextual
    /// keyword surface, not just the 7-word subset the previous inline
    /// implementation handled.
    /// </summary>
    public static string SafeIdentifier(string name, int parameterIndex = -1)
    {
        if (string.IsNullOrEmpty(name))
        {
            return parameterIndex >= 0 ? $"@_p{parameterIndex}" : "@_";
        }
        return ReservedKeywords.Contains(name) ? "@" + name : name;
    }
}
