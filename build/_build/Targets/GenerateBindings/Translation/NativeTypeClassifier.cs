using Build.Targets.GenerateBindings.Model;
using CppAst;

namespace Build.Targets.GenerateBindings.Translation;

/// <summary>
/// Classifies CppAst types into <see cref="NativeTypeRef"/> semantic descriptors.
/// This is the entry point for the semantic binding pipeline (Phase 4) — it
/// replaces ad-hoc string-based heuristics with structural CppAst inspection.
/// <para>
/// Responsibilities: opaque handle vs concrete struct discrimination, explicit
/// typedef recognition (SDL_bool → int), UTF-8 string pointer detection,
/// void* handling, and unsupported-type diagnostics. Does not touch existing
/// <see cref="TypeMappingPolicy"/> or the Stage 1 emitters.
/// </para>
/// </summary>
internal sealed class NativeTypeClassifier
{
    private readonly NativeTypeClassificationContext _context;

    public NativeTypeClassifier(NativeTypeClassificationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
    }

    public bool IsOwnedNativeName(string nativeName) => _context.IsOwned(nativeName);

    /// <summary>
    /// Top-level dispatch. Unwraps <see cref="CppQualifiedType"/> before dispatching
    /// by concrete shape. Unsupported shapes produce a diagnostic rather than silently
    /// emitting <c>nint</c>.
    /// </summary>
    public NativeTypeRef Classify(CppType type, string? sourceHeader)
    {
        ArgumentNullException.ThrowIfNull(type);
        type = UnwrapQualified(type);

        return type switch
        {
            CppClass cls => Classify(cls, sourceHeader),
            CppTypedef td => Classify(td, sourceHeader),
            CppPointerType ptr => Classify(ptr, sourceHeader),
            CppArrayType array => ClassifyArray(array, sourceHeader),
            CppFunctionType function => FunctionPointer(function, sourceHeader),
            CppPrimitiveType prim => ClassifyPrimitive(prim, sourceHeader),
            CppEnum enm => ClassifyEnum(enm, sourceHeader),
            _ => Unsupported(type.GetDisplayName(), sourceHeader,
                $"Unsupported CppType shape: {type.GetType().Name}"),
        };
    }

    /// <summary>
    /// Classifies a <see cref="CppClass"/>: forward declarations and zero-size,
    /// field-free definitions become <see cref="NativeTypeKind.OpaqueHandle"/>; all
    /// other definitions become <see cref="NativeTypeKind.ConcreteStruct"/>.
    /// </summary>
    public NativeTypeRef Classify(CppClass cls, string? sourceHeader)
    {
        ArgumentNullException.ThrowIfNull(cls);
        var owningFamilyId = _context.IsOwned(cls.Name) ? _context.FamilyId : null;

        if (ExternalNativeTypePolicy.TryMapExternalOpaqueName(cls.Name, out var externalOpaqueManaged))
        {
            return ExternalOpaque(cls.Name, externalOpaqueManaged, sourceHeader, pointerDepth: 0, elementType: null);
        }

        if (_context.IsDeferred(cls.Name))
        {
            return new NativeTypeRef(cls.Name, "nint", NativeTypeKind.Deferred, 0, owningFamilyId, sourceHeader,
                NativeAbiShape.Of("nint", IntPtr.Size), null, []);
        }

        if (SdlNativeTypeSubstitutionPolicy.TryMapName(cls.Name, out var substitutedManaged))
        {
            return new NativeTypeRef(cls.Name, substitutedManaged, NativeTypeKind.SubstitutedManagedType, 0,
                owningFamilyId, sourceHeader, NativeAbiShape.Of(substitutedManaged), null, []);
        }

        if (!cls.IsDefinition || (cls.SizeOf == 0 && cls.Fields.Count == 0))
        {
            return NativeTypeRef.OpaqueHandle(cls.Name, cls.Name, owningFamilyId, sourceHeader);
        }

        return NativeTypeRef.ConcreteStruct(cls.Name, cls.Name, owningFamilyId, sourceHeader);
    }

    /// <summary>
    /// Classifies a <see cref="CppTypedef"/>. Checks the explicit map first
    /// (SDL_bool → int, Sint8 → sbyte …), then name-based substitution
    /// (SDL_GUID → Guid), then recurses through the element type chain.
    /// </summary>
    public NativeTypeRef Classify(CppTypedef typedef, string? sourceHeader)
    {
        ArgumentNullException.ThrowIfNull(typedef);
        return ClassifyTypedef(typedef, sourceHeader, depth: 0);
    }

    /// <summary>
    /// Classifies a <see cref="CppPointerType"/>:
    /// <list type="bullet">
    ///   <item>void* → <see cref="NativeTypeKind.VoidPointer"/> (nint)</item>
    ///   <item>char* / unsigned char* → <see cref="NativeTypeKind.Utf8Pointer"/> (byte*)</item>
    ///   <item>other primitive* → <see cref="NativeTypeKind.TypedPointer"/></item>
    ///   <item>class/typedef pointees → <see cref="NativeTypeKind.TypedPointer"/> via element classification</item>
    /// </list>
    /// </summary>
    public NativeTypeRef Classify(CppPointerType pointer, string? sourceHeader) => ClassifyPointer(pointer, sourceHeader);

    private NativeTypeRef ClassifyTypedef(CppTypedef typedef, string? sourceHeader, int depth)
    {
        if (depth > TypeMappingPolicy.MaxTypedefDepth)
        {
            return Unsupported(typedef.Name, sourceHeader,
                $"Typedef chain depth exceeded {TypeMappingPolicy.MaxTypedefDepth} resolving '{typedef.Name}'.");
        }

        var owningFamilyId = _context.IsOwned(typedef.Name) ? _context.FamilyId : null;

        if (_context.IsDeferred(typedef.Name))
        {
            return new NativeTypeRef(typedef.Name, "nint", NativeTypeKind.Deferred, 0,
                owningFamilyId, sourceHeader, NativeAbiShape.Of("nint", IntPtr.Size), null, []);
        }

        if (TypeMappingPolicy.TryMapExplicitTypedef(typedef.Name, out var explicitManaged))
        {
            return new NativeTypeRef(typedef.Name, explicitManaged, NativeTypeKind.ValueTypedef, 0,
                owningFamilyId, sourceHeader,
                NativeAbiShape.Of(explicitManaged, PrimitiveSizeBytes(explicitManaged)),
                null, []);
        }

        if (SdlNativeTypeSubstitutionPolicy.TryMapName(typedef.Name, out var substitutedManaged))
        {
            return new NativeTypeRef(typedef.Name, substitutedManaged, NativeTypeKind.SubstitutedManagedType, 0,
                owningFamilyId, sourceHeader,
                NativeAbiShape.Of(substitutedManaged),
                null, []);
        }

        if (ExternalNativeTypePolicy.TryMapTypedefName(typedef.Name, out var externalManaged))
        {
            return new NativeTypeRef(typedef.Name, externalManaged, NativeTypeKind.SubstitutedManagedType, 0,
                owningFamilyId, sourceHeader,
                NativeAbiShape.Of(externalManaged),
                null, []);
        }

        if (ExternalNativeTypePolicy.TryMapExternalOpaqueName(typedef.Name, out var externalOpaqueManaged))
        {
            return ExternalOpaque(typedef.Name, externalOpaqueManaged, sourceHeader, pointerDepth: 0, elementType: null);
        }

        if (TryGetFunctionPointerType(typedef.ElementType, out _))
        {
            return FunctionPointer(typedef.Name, sourceHeader, pointerDepth: 1, elementType: null, owningFamilyId);
        }

        var element = UnwrapQualified(typedef.ElementType);

        var classifiedElement = element switch
        {
            CppPrimitiveType prim => ClassifyPrimitive(prim, sourceHeader),
            CppEnum enm => ClassifyEnum(enm, sourceHeader),
            CppTypedef nested => ClassifyTypedef(nested, sourceHeader, depth + 1),
            CppClass cls => Classify(cls, sourceHeader),
            CppPointerType pointer => ClassifyPointer(pointer, sourceHeader),
            _ => Unsupported(typedef.Name, sourceHeader,
                $"Unresolvable typedef '{typedef.Name}' with element type {element.GetType().Name}"),
        };

        return RebindTypedef(typedef.Name, classifiedElement, owningFamilyId, sourceHeader);
    }

    private static NativeTypeRef ClassifyPrimitive(CppPrimitiveType prim, string? sourceHeader)
    {
        var native = NativePrimitiveName(prim);
        var managed = TypeMappingPolicy.MapPrimitive(prim).ManagedName;
        return NativeTypeRef.Primitive(native, managed,
            NativeAbiShape.Of(managed, PrimitiveSizeBytes(managed)), sourceHeader);
    }

    private NativeTypeRef ClassifyEnum(CppEnum enm, string? sourceHeader)
    {
        ArgumentNullException.ThrowIfNull(enm);
        var owningFamilyId = _context.IsOwned(enm.Name) ? _context.FamilyId : null;

        return new NativeTypeRef(enm.Name, "int", NativeTypeKind.Enum, 0, owningFamilyId, sourceHeader,
            NativeAbiShape.Of("int", 4), null, []);
    }

    private static NativeTypeRef Unsupported(string nativeName, string? sourceHeader, string message) =>
        new(nativeName, "nint", NativeTypeKind.Unsupported, 0, null, sourceHeader,
            NativeAbiShape.Of("nint", IntPtr.Size), null,
            [new NativeTypeDiagnostic(NativeTypeDiagnosticSeverity.Warning, message, sourceHeader, nativeName)]);

    private static NativeTypeRef FunctionPointer(CppFunctionType functionType, string? sourceHeader) =>
        FunctionPointer(functionType.GetDisplayName(), sourceHeader, pointerDepth: 0, elementType: null, owningFamilyId: null);

    private static NativeTypeRef FunctionPointer(
        string nativeName,
        string? sourceHeader,
        int pointerDepth,
        NativeTypeRef? elementType,
        string? owningFamilyId = null) =>
        new(nativeName, FunctionPointerManagedName(pointerDepth), NativeTypeKind.FunctionPointer, pointerDepth,
            owningFamilyId, sourceHeader, NativeAbiShape.Of("IntPtr", IntPtr.Size), elementType, elementType?.Diagnostics ?? []);

    private static NativeTypeRef ExternalOpaque(
        string nativeName,
        string managedName,
        string? sourceHeader,
        int pointerDepth,
        NativeTypeRef? elementType) =>
        new(nativeName, managedName, NativeTypeKind.ExternalOpaque, pointerDepth, null, sourceHeader,
            NativeAbiShape.Of(managedName, IntPtr.Size), elementType, elementType?.Diagnostics ?? []);

    private static NativeTypeRef RebindTypedef(
        string typedefName,
        NativeTypeRef element,
        string? owningFamilyId,
        string? sourceHeader)
    {
        var kind = element.Kind is NativeTypeKind.Primitive or NativeTypeKind.Enum
            ? NativeTypeKind.ValueTypedef
            : element.Kind;
        var managedName = kind is NativeTypeKind.OpaqueHandle or NativeTypeKind.ConcreteStruct or NativeTypeKind.Union
            ? typedefName
            : element.ManagedName;

        return element with
        {
            NativeName = typedefName,
            ManagedName = managedName,
            Kind = kind,
            OwningFamilyId = owningFamilyId ?? element.OwningFamilyId,
            SourceHeader = sourceHeader,
        };
    }

    private NativeTypeRef ClassifyPointer(CppType pointerType, string? sourceHeader)
    {
        ArgumentNullException.ThrowIfNull(pointerType);

        var pointerDepth = 0;
        var element = UnwrapQualified(pointerType);
        while (element is CppPointerType pointer)
        {
            pointerDepth++;
            element = UnwrapQualified(pointer.ElementType);
        }

        if (pointerDepth <= 0)
        {
            throw new ArgumentException("Type must contain at least one pointer layer.", nameof(pointerType));
        }

        var elementRef = ClassifyPointerElement(element, sourceHeader);

        if (elementRef.Kind == NativeTypeKind.Unsupported)
        {
            return elementRef;
        }

        if (elementRef.Kind is NativeTypeKind.FunctionPointer)
        {
            return FunctionPointer(
                elementRef.NativeName,
                sourceHeader,
                pointerDepth + elementRef.PointerDepth,
                elementRef,
                elementRef.OwningFamilyId);
        }

        if (elementRef.PointerDepth > 0)
        {
            pointerDepth += elementRef.PointerDepth;
            elementRef = elementRef.Kind == NativeTypeKind.VoidPointer && elementRef.ElementType is null
                ? NativeTypeRef.Primitive("void", "void", NativeAbiShape.Of("void"), sourceHeader)
                : elementRef.ElementType ?? Unsupported(elementRef.NativeName, sourceHeader,
                    $"Pointer typedef '{elementRef.NativeName}' has no base element metadata.");
        }

        return elementRef.Kind == NativeTypeKind.Unsupported
            ? elementRef
            : CreatePointerRef(elementRef, pointerDepth, sourceHeader);
    }

    private NativeTypeRef ClassifyPointerElement(CppType element, string? sourceHeader)
    {
        element = UnwrapQualified(element);
        return element switch
        {
            CppPrimitiveType prim => ClassifyPointerPrimitiveElement(prim, sourceHeader),
            CppClass cls => Classify(cls, sourceHeader),
            CppTypedef td => Classify(td, sourceHeader),
            CppEnum enm => ClassifyEnum(enm, sourceHeader),
            CppFunctionType function => FunctionPointer(function, sourceHeader),
            _ => Unsupported(element.GetDisplayName(), sourceHeader,
                $"Unsupported pointer element type: {element.GetType().Name}"),
        };
    }

    private NativeTypeRef ClassifyArray(CppArrayType array, string? sourceHeader)
    {
        if (array.Size >= 0)
        {
            return Unsupported(array.GetDisplayName(), sourceHeader,
                $"Fixed-size array type should be translated by the owning declaration before classification: {array.GetDisplayName()}");
        }

        return ClassifyPointer(new CppPointerType(array.ElementType), sourceHeader);
    }

    private static NativeTypeRef ClassifyPointerPrimitiveElement(CppPrimitiveType prim, string? sourceHeader)
    {
        var native = NativePrimitiveName(prim);
        var managed = prim.Kind is CppPrimitiveKind.Char or CppPrimitiveKind.UnsignedChar
            ? "byte"
            : TypeMappingPolicy.MapPrimitive(prim).ManagedName;
        return NativeTypeRef.Primitive(native, managed,
            NativeAbiShape.Of(managed, PrimitiveSizeBytes(managed)), sourceHeader);
    }

    private static NativeTypeRef CreatePointerRef(NativeTypeRef elementRef, int pointerDepth, string? sourceHeader)
    {
        if (elementRef.NativeName == "void")
        {
            if (pointerDepth == 1)
            {
                return new NativeTypeRef("void*", "nint", NativeTypeKind.VoidPointer, 1, null, sourceHeader,
                    NativeAbiShape.Of("nint", IntPtr.Size), elementRef, elementRef.Diagnostics);
            }

            return NativeTypeRef.Indirection(elementRef, pointerDepth, "nint" + RepeatStars(pointerDepth - 1));
        }

        if (elementRef.Kind == NativeTypeKind.ExternalOpaque)
        {
            return pointerDepth == 1
                ? ExternalOpaque(elementRef.NativeName + "*", elementRef.ManagedName, sourceHeader, pointerDepth, elementRef)
                : NativeTypeRef.Indirection(elementRef, pointerDepth, elementRef.ManagedName + RepeatStars(pointerDepth - 1));
        }

        if (IsCharPrimitive(elementRef))
        {
            if (pointerDepth == 1)
            {
                return new NativeTypeRef(elementRef.NativeName + "*", "byte*", NativeTypeKind.Utf8Pointer, 1,
                    null, sourceHeader, NativeAbiShape.Of("nint", IntPtr.Size), elementRef, elementRef.Diagnostics);
            }

            return NativeTypeRef.Indirection(elementRef, pointerDepth, "byte" + RepeatStars(pointerDepth));
        }

        return NativeTypeRef.Indirection(elementRef, pointerDepth, PointerManagedName(elementRef, pointerDepth));
    }

    private static string PointerManagedName(NativeTypeRef elementRef, int pointerDepth) =>
        elementRef.Kind == NativeTypeKind.OpaqueHandle
            ? elementRef.ManagedName + RepeatStars(pointerDepth - 1)
            : elementRef.ManagedName + RepeatStars(pointerDepth);

    private static bool IsCharPrimitive(NativeTypeRef elementRef) =>
        elementRef.Kind == NativeTypeKind.Primitive &&
        elementRef.NativeName is "char" or "unsigned char";

    private static CppType UnwrapQualified(CppType type)
    {
        while (type is CppQualifiedType qualified)
            type = qualified.ElementType;

        return type;
    }

    private static bool TryGetFunctionPointerType(CppType type, out CppFunctionType functionType)
    {
        type = UnwrapQualified(type);
        if (type is CppPointerType pointer)
        {
            type = UnwrapQualified(pointer.ElementType);
        }

        if (type is CppFunctionType function)
        {
            functionType = function;
            return true;
        }

        functionType = null!;
        return false;
    }

    private static string RepeatStars(int count) =>
        count <= 0 ? string.Empty : new string('*', count);

    private static string FunctionPointerManagedName(int pointerDepth) =>
        pointerDepth <= 1 ? "IntPtr" : "IntPtr" + RepeatStars(pointerDepth - 1);

    private static string NativePrimitiveName(CppPrimitiveType primitive) => primitive.Kind switch
    {
        CppPrimitiveKind.Void => "void",
        CppPrimitiveKind.Bool => "bool",
        CppPrimitiveKind.Char => "char",
        CppPrimitiveKind.WChar => "wchar_t",
        CppPrimitiveKind.Short => "short",
        CppPrimitiveKind.Int => "int",
        CppPrimitiveKind.Long => "long",
        CppPrimitiveKind.LongLong => "long long",
        CppPrimitiveKind.UnsignedChar => "unsigned char",
        CppPrimitiveKind.UnsignedShort => "unsigned short",
        CppPrimitiveKind.UnsignedInt => "unsigned int",
        CppPrimitiveKind.UnsignedLong => "unsigned long",
        CppPrimitiveKind.UnsignedLongLong => "unsigned long long",
        CppPrimitiveKind.Float => "float",
        CppPrimitiveKind.Double => "double",
        _ => primitive.GetDisplayName(),
    };

    private static int? PrimitiveSizeBytes(string managedName) => managedName switch
    {
        "sbyte" or "byte" => 1,
        "short" or "ushort" => 2,
        "int" or "uint" or "float" => 4,
        "long" or "ulong" or "double" => 8,
        "nint" or "nuint" => IntPtr.Size,
        _ => null,
    };
}
