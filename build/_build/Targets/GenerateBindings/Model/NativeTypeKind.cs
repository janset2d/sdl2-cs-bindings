namespace Build.Targets.GenerateBindings.Model;

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
