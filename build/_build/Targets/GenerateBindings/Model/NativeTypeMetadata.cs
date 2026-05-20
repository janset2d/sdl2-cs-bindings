namespace Build.Targets.GenerateBindings.Model;

public sealed record NativeAbiShape(string StorageName, int? SizeBytes, bool IsBlittable)
{
    public static NativeAbiShape Of(string storageName, int? sizeBytes = null, bool isBlittable = true) =>
        new(storageName, sizeBytes, isBlittable);
}

public enum NativeTypeKind
{
    Primitive,
    Enum,
    FlagsEnum,
    ValueTypedef,
    ConcreteStruct,
    Union,
    OpaqueHandle,
    VoidPointer,
    Utf8Pointer,
    TypedPointer,
    FunctionPointer,
    Callback,
    Array,
    ExternalOpaque,
    SubstitutedManagedType,
    Deferred,
    Unsupported,
}

public sealed record NativeTypeDiagnostic(
    NativeTypeDiagnosticSeverity Severity,
    string Message,
    string? SourceHeader,
    string? NativeName);

public enum NativeTypeDiagnosticSeverity
{
    Error,
    Warning,
}
