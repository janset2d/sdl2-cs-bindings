using System.Collections.Frozen;
using Build.Targets.GenerateBindings.Model;
using CppAst;

namespace Build.Targets.GenerateBindings.ModelBuilding.SdlPolicy;

internal static class SdlNativeTypeSubstitutionPolicy
{
    private static readonly FrozenDictionary<string, string> ValueTypeMappings =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["SDL_GUID"] = "Guid",
        }.ToFrozenDictionary(StringComparer.Ordinal);

    public static bool IsSubstitutedValueType(string name) =>
        ValueTypeMappings.ContainsKey(name);

    /// <summary>
    /// Name-keyed lookup that returns the managed name string directly, without
    /// constructing a <see cref="BindingTypeRef"/>. Used by
    /// <see cref="NativeTypeClassifier"/> during type classification.
    /// </summary>
    public static bool TryMapName(string name, out string managedName) =>
        ValueTypeMappings.TryGetValue(name, out managedName!);

    public static bool TryMap(CppClass cls, out BindingTypeRef mapped)
    {
        ArgumentNullException.ThrowIfNull(cls);
        return TryMapName(cls.Name, out mapped);
    }

    public static bool TryMap(CppTypedef typedef, out BindingTypeRef mapped)
    {
        ArgumentNullException.ThrowIfNull(typedef);
        return TryMapName(typedef.Name, out mapped);
    }

    private static bool TryMapName(string name, out BindingTypeRef mapped)
    {
        if (ValueTypeMappings.TryGetValue(name, out var managedName))
        {
            mapped = BindingTypeRef.Of(managedName);
            return true;
        }

        mapped = default!;
        return false;
    }
}
