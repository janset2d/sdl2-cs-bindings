using System.Collections.Frozen;
using CppAst;

namespace Janset.Sdl2.AlimerSpike.Generator;

internal enum SkipReason
{
    None,
    UnsupportedValueType,
    FunctionPointer,
    Variadic,
    WideStringPointer,
    UnknownPrimitive,
}

internal readonly record struct MappedType(string? ManagedName, SkipReason Reason)
{
    public bool IsSkip => Reason != SkipReason.None;
    public static MappedType Of(string managed) => new(managed, SkipReason.None);
    public static MappedType Skip(SkipReason reason) => new(null, reason);
}

// Direct port of the subset of
// build/_build/Targets/GenerateBindings/ModelBuilding/Types/TypeMappingPolicy.cs
// that the spike actually needs: explicit SDL2 typedef widths, C primitive
// widths (including the C long → CLong policy from constitution
// §"Scalar Type Translation"), and a pointer rule that maps SDL_-prefixed
// opaque pointees to nint while non-SDL pointees take a managed-pointer form.
//
// Spike intentionally skips structs-by-value, function pointers, variadic
// functions, and unknown shapes rather than inventing fake mappings — same
// stance as Cake's KnownUnsupportedDeclarationPolicy.
internal static class Sdl2TypeNameMapper
{
    private const int MaxTypedefDepth = 16;

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
            ["SDL_GUID"] = "Guid",
        }.ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly FrozenSet<string> CSharpReservedKeywords =
        new HashSet<string>(StringComparer.Ordinal)
        {
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
            "base", "params", "ref", "in", "out",
        }.ToFrozenSet(StringComparer.Ordinal);

    public static string SafeIdentifier(string name, int parameterIndex)
    {
        if (string.IsNullOrEmpty(name))
        {
            return $"@_p{parameterIndex}";
        }
        return CSharpReservedKeywords.Contains(name) ? "@" + name : name;
    }

    public static MappedType Map(CppType type) => Map(type, depth: 0);

    private static MappedType Map(CppType type, int depth)
    {
        if (depth > MaxTypedefDepth)
        {
            return MappedType.Skip(SkipReason.UnknownPrimitive);
        }

        while (type is CppQualifiedType qt)
        {
            type = qt.ElementType;
        }

        return type switch
        {
            CppPrimitiveType prim => MapPrimitive(prim),
            CppPointerType pointer => MapPointer(pointer, depth + 1),
            CppTypedef td => MapTypedef(td, depth + 1),
            CppArrayType arr => MapPointer(new CppPointerType(arr.ElementType), depth + 1),
            CppEnum => MappedType.Of("int"),
            CppClass cls => MapClassByValue(cls),
            CppFunctionType => MappedType.Skip(SkipReason.FunctionPointer),
            _ => MappedType.Skip(SkipReason.UnknownPrimitive),
        };
    }

    private static MappedType MapPrimitive(CppPrimitiveType prim) =>
        prim.Kind switch
        {
            CppPrimitiveKind.Void => MappedType.Of("void"),
            CppPrimitiveKind.Bool => MappedType.Of("byte"),
            CppPrimitiveKind.Char => MappedType.Of("sbyte"),
            CppPrimitiveKind.WChar => MappedType.Skip(SkipReason.WideStringPointer),
            CppPrimitiveKind.Short => MappedType.Of("short"),
            CppPrimitiveKind.Int => MappedType.Of("int"),
            CppPrimitiveKind.LongLong => MappedType.Of("long"),
            CppPrimitiveKind.UnsignedChar => MappedType.Of("byte"),
            CppPrimitiveKind.UnsignedShort => MappedType.Of("ushort"),
            CppPrimitiveKind.UnsignedInt => MappedType.Of("uint"),
            CppPrimitiveKind.UnsignedLongLong => MappedType.Of("ulong"),
            CppPrimitiveKind.Float => MappedType.Of("float"),
            CppPrimitiveKind.Double => MappedType.Of("double"),
            CppPrimitiveKind.Long => MappedType.Of("CLong"),
            CppPrimitiveKind.UnsignedLong => MappedType.Of("CULong"),
            _ => MappedType.Skip(SkipReason.UnknownPrimitive),
        };

    private static MappedType MapPointer(CppPointerType pointer, int depth)
    {
        var element = pointer.ElementType;
        while (element is CppQualifiedType qt)
        {
            element = qt.ElementType;
        }

        switch (element)
        {
            case CppPrimitiveType { Kind: CppPrimitiveKind.Void }:
                return MappedType.Of("nint");

            case CppPrimitiveType { Kind: CppPrimitiveKind.WChar }:
                return MappedType.Of("nint");

            case CppPrimitiveType prim:
                var primMapped = MapPrimitive(prim);
                return primMapped.IsSkip ? primMapped : MappedType.Of(primMapped.ManagedName + "*");

            case CppTypedef td when td.Name.StartsWith("SDL_", StringComparison.Ordinal):
                return MappedType.Of("nint");

            case CppTypedef td:
                var typedefMapped = MapTypedef(td, depth + 1);
                return typedefMapped.IsSkip ? typedefMapped : MappedType.Of(typedefMapped.ManagedName + "*");

            case CppClass cls when cls.Name.StartsWith("SDL_", StringComparison.Ordinal):
                return MappedType.Of("nint");

            case CppClass cls when string.IsNullOrEmpty(cls.Name):
                return MappedType.Of("nint");

            case CppFunctionType:
                return MappedType.Skip(SkipReason.FunctionPointer);

            default:
                return MappedType.Skip(SkipReason.UnsupportedValueType);
        }
    }

    private static MappedType MapTypedef(CppTypedef td, int depth)
    {
        if (ExplicitTypedefMap.TryGetValue(td.Name, out var explicitMapping))
        {
            return MappedType.Of(explicitMapping);
        }

        var element = td.ElementType;
        while (element is CppQualifiedType qt)
        {
            element = qt.ElementType;
        }

        return element switch
        {
            CppPrimitiveType prim => MapPrimitive(prim),
            CppTypedef nested => MapTypedef(nested, depth + 1),
            CppEnum => MappedType.Of("int"),
            CppPointerType pointer => MapPointer(pointer, depth + 1),
            _ when td.Name.StartsWith("SDL_", StringComparison.Ordinal) => MappedType.Of("nint"),
            _ => Map(td.ElementType, depth + 1),
        };
    }

    private static MappedType MapClassByValue(CppClass cls)
    {
        // The spike does not yet emit struct definitions; struct-by-value
        // parameters and returns need the type to be a known POD layout. Skip
        // until a struct emitter pass exists.
        return MappedType.Skip(SkipReason.UnsupportedValueType);
    }
}
