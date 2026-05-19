namespace Build.Targets.GenerateBindings.Model;

public sealed record NativeTypeRef(
    string NativeName,
    string ManagedName,
    NativeTypeKind Kind,
    int PointerDepth,
    string? OwningFamilyId,
    string? SourceHeader,
    NativeAbiShape AbiShape,
    NativeTypeRef? ElementType,
    IReadOnlyList<NativeTypeDiagnostic> Diagnostics)
{
    public static NativeTypeRef Primitive(string nativeName, string managedName, NativeAbiShape abiShape, string? sourceHeader = null) =>
        new(nativeName, managedName, NativeTypeKind.Primitive, 0, null, sourceHeader, abiShape, null, []);

    public static NativeTypeRef ConcreteStruct(string nativeName, string managedName, string? owningFamilyId, string? sourceHeader) =>
        new(nativeName, managedName, NativeTypeKind.ConcreteStruct, 0, owningFamilyId, sourceHeader, NativeAbiShape.Of(managedName), null, []);

    public static NativeTypeRef OpaqueHandle(string nativeName, string managedName, string? owningFamilyId, string? sourceHeader) =>
        new(nativeName, managedName, NativeTypeKind.OpaqueHandle, 0, owningFamilyId, sourceHeader, NativeAbiShape.Of("nint", IntPtr.Size), null, []);

    public static NativeTypeRef Indirection(NativeTypeRef elementType, int indirectionDepth, string managedName)
    {
        if (indirectionDepth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(indirectionDepth), "Indirection depth must be positive.");
        }

        if (elementType.PointerDepth != 0)
        {
            throw new ArgumentException("Element type must be a non-pointer base type.", nameof(elementType));
        }

        return new(elementType.NativeName, managedName, NativeTypeKind.TypedPointer, indirectionDepth, elementType.OwningFamilyId, elementType.SourceHeader, NativeAbiShape.Of("nint", IntPtr.Size), elementType, elementType.Diagnostics);
    }
}
