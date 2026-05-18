using System.Collections.Frozen;
using Build.Targets.GenerateBindings.Model;
using CppAst;

namespace Build.Targets.GenerateBindings.Translation;

internal static class SdlNativeTypeSubstitutionPolicy
{
    private static readonly FrozenDictionary<string, string> ValueTypeMappings =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["SDL_GUID"] = "Guid",
        }.ToFrozenDictionary(StringComparer.Ordinal);

    public static bool IsSubstitutedValueType(string name) =>
        ValueTypeMappings.ContainsKey(name);

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
