using Build.Targets.GenerateBindings.Model;
using CppAst;

namespace Build.Targets.GenerateBindings.ModelBuilding.SdlPolicy;

internal static class SdlWideStringPointerPolicy
{
    public static bool TryField(string? structName, string fieldName, CppType type, string? sourceHeader, out NativeTypeRef result)
    {
        if (structName == "SDL_hid_device_info" &&
            fieldName is "serial_number" or "manufacturer_string" or "product_string" &&
            IsWidePointerShape(type))
        {
            result = WidePointer(sourceHeader);
            return true;
        }

        result = null!;
        return false;
    }

    public static bool TryParameter(string functionName, string parameterName, CppType type, string? sourceHeader, out NativeTypeRef result)
    {
        if (IsKnownWideStringParameter(functionName, parameterName) && IsWidePointerShape(type))
        {
            result = WidePointer(sourceHeader);
            return true;
        }

        result = null!;
        return false;
    }

    private static bool IsKnownWideStringParameter(string functionName, string parameterName) =>
        (functionName, parameterName) switch
        {
            ("SDL_hid_open", "serial_number") => true,
            ("SDL_hid_get_manufacturer_string", "string") => true,
            ("SDL_hid_get_product_string", "string") => true,
            ("SDL_hid_get_serial_number_string", "string") => true,
            ("SDL_hid_get_indexed_string", "string") => true,
            _ => false,
        };

    private static NativeTypeRef WidePointer(string? sourceHeader) =>
        new("wchar_t*", "nint", NativeTypeKind.TypedPointer, 1, null, sourceHeader,
            NativeAbiShape.Of("nint", IntPtr.Size), null, []);

    private static bool IsWidePointerShape(CppType type)
    {
        type = UnwrapQualified(type);
        return type is CppPointerType pointer && IsWideElementShape(pointer.ElementType);
    }

    private static bool IsWideElementShape(CppType type)
    {
        type = UnwrapQualified(type);
        return type switch
        {
            CppPrimitiveType { Kind: CppPrimitiveKind.Int or CppPrimitiveKind.WChar } => true,
            CppTypedef typedef => typedef.Name == "wchar_t" || IsWideElementShape(typedef.ElementType),
            _ => false,
        };
    }

    private static CppType UnwrapQualified(CppType type)
    {
        while (type is CppQualifiedType qualified)
        {
            type = qualified.ElementType;
        }

        return type;
    }
}
