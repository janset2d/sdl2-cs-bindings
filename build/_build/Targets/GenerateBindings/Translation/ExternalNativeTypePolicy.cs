using System.Collections.Frozen;
using Build.Targets.GenerateBindings.Model;
using CppAst;

namespace Build.Targets.GenerateBindings.Translation;

/// <summary>
/// Explicit policy for non-SDL native types that appear in SDL2 signatures. This
/// keeps compile-green pressure from leaking ABI-unknown names such as
/// <c>__va_list_tag</c>, <c>_IO_FILE</c>, or Vulkan private handle structs into
/// generated C#.
/// </summary>
internal static class ExternalNativeTypePolicy
{
    private static readonly FrozenDictionary<string, string> MappedTypedefs =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["VkInstance"] = "IntPtr",
            ["VkSurfaceKHR"] = "ulong",
            ["XUserHandle"] = "IntPtr",
            ["XTaskQueueHandle"] = "IntPtr",
        }.ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly FrozenDictionary<string, string> ExternalOpaqueTypes =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["va_list"] = "IntPtr",
            ["__va_list_tag"] = "IntPtr",
            ["FILE"] = "IntPtr",
            ["_IO_FILE"] = "IntPtr",
            ["SDL_iconv_t"] = "IntPtr",
            ["_SDL_iconv_t"] = "IntPtr",
            ["ID3D11Device"] = "IntPtr",
            ["ID3D12Device"] = "IntPtr",
            ["IDirect3DDevice9"] = "IntPtr",
        }.ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly FrozenSet<string> DeferredNativeTypes =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "va_list",
            "__va_list_tag",
            "FILE",
            "_IO_FILE",
        }.ToFrozenSet(StringComparer.Ordinal);

    public static bool TryMapTypedef(CppTypedef typedef, out BindingTypeRef mapped)
    {
        ArgumentNullException.ThrowIfNull(typedef);

        if (MappedTypedefs.TryGetValue(typedef.Name, out var managedName))
        {
            mapped = BindingTypeRef.Of(managedName);
            return true;
        }

        mapped = default!;
        return false;
    }

    /// <summary>
    /// Name-keyed lookup that returns the managed name string directly, without
    /// constructing a <see cref="BindingTypeRef"/>. Used by
    /// <see cref="NativeTypeClassifier"/> during type classification.
    /// </summary>
    public static bool TryMapTypedefName(string name, out string managedName) =>
        MappedTypedefs.TryGetValue(name, out managedName!);

    public static bool TryMapExternalOpaqueName(string name, out string managedName) =>
        ExternalOpaqueTypes.TryGetValue(name, out managedName!);

    public static bool TryMapTypedefPointer(CppTypedef typedef, out BindingTypeRef mapped)
    {
        ArgumentNullException.ThrowIfNull(typedef);

        if (DeferredNativeTypes.Contains(typedef.Name))
        {
            mapped = BindingTypeRef.Of("IntPtr");
            return true;
        }

        if (MappedTypedefs.TryGetValue(typedef.Name, out var managedName))
        {
            mapped = BindingTypeRef.Of(managedName + "*");
            return true;
        }

        mapped = default!;
        return false;
    }

    public static bool TryMapClassPointer(CppClass cls, out BindingTypeRef mapped)
    {
        ArgumentNullException.ThrowIfNull(cls);

        if (DeferredNativeTypes.Contains(cls.Name))
        {
            mapped = BindingTypeRef.Of("IntPtr");
            return true;
        }

        mapped = default!;
        return false;
    }

    public static bool TryGetDeferredReason(CppFunction function, out string reason)
    {
        ArgumentNullException.ThrowIfNull(function);

        if (TryFindDeferredNativeType(function.ReturnType, out var returnTypeName))
        {
            reason = FormatDeferredReason(function.Name, returnTypeName, "return type");
            return true;
        }

        foreach (var parameter in function.Parameters)
        {
            if (TryFindDeferredNativeType(parameter.Type, out var parameterTypeName))
            {
                var parameterName = string.IsNullOrWhiteSpace(parameter.Name) ? "<unnamed>" : parameter.Name;
                reason = FormatDeferredReason(function.Name, parameterTypeName, $"parameter '{parameterName}'");
                return true;
            }
        }

        reason = string.Empty;
        return false;
    }

    private static string FormatDeferredReason(string functionName, string nativeTypeName, string location) =>
        $"deferred external native type '{nativeTypeName}' in {functionName} {location}: " +
        "Stage 1 has no portable ABI-safe mapping for this C runtime type.";

    private static bool TryFindDeferredNativeType(CppType type, out string typeName) =>
        TryFindDeferredNativeType(UnwrapQualified(type), depth: 0, out typeName);

    private static bool TryFindDeferredNativeType(CppType type, int depth, out string typeName)
    {
        if (depth > TypeMappingPolicy.MaxTypedefDepth)
        {
            throw new InvalidOperationException(
                $"Native type deferral scan exceeded {TypeMappingPolicy.MaxTypedefDepth} levels resolving '{type}'.");
        }

        type = UnwrapQualified(type);

        switch (type)
        {
            case CppTypedef typedef:
                if (DeferredNativeTypes.Contains(typedef.Name))
                {
                    typeName = typedef.Name;
                    return true;
                }

                return TryFindDeferredNativeType(typedef.ElementType, depth + 1, out typeName);

            case CppPointerType pointer:
                return TryFindDeferredNativeType(pointer.ElementType, depth + 1, out typeName);

            case CppArrayType array:
                return TryFindDeferredNativeType(array.ElementType, depth + 1, out typeName);

            case CppClass cls when DeferredNativeTypes.Contains(cls.Name):
                typeName = cls.Name;
                return true;

            default:
                typeName = string.Empty;
                return false;
        }
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
