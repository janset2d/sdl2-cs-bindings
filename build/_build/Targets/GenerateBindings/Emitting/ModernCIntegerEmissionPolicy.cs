using Build.Targets.GenerateBindings.Model;

namespace Build.Targets.GenerateBindings.Emitting;

internal static class ModernCIntegerEmissionPolicy
{
    public const string Guard = "NET6_0_OR_GREATER";

    public static bool UsesModernCInteger(NativeTypeRef type) =>
        IsModernCIntegerName(type.ManagedName) ||
        (type.ElementType is not null && UsesModernCInteger(type.ElementType));

    public static bool UsesModernCInteger(BindingFunction function) =>
        UsesModernCInteger(function.ReturnType) ||
        function.Parameters.Any(parameter => UsesModernCInteger(parameter.Type));

    public static bool UsesModernCInteger(BindingCallback callback) =>
        UsesModernCInteger(callback.ReturnType) ||
        callback.Parameters.Any(parameter => UsesModernCInteger(parameter.Type));

    private static bool IsModernCIntegerName(string managedName) =>
        managedName is "CLong" or "CULong" ||
        IsPointerToModernCInteger(managedName, "CLong") ||
        IsPointerToModernCInteger(managedName, "CULong");

    private static bool IsPointerToModernCInteger(string managedName, string typeName) =>
        managedName.StartsWith(typeName, StringComparison.Ordinal) &&
        managedName[typeName.Length..].All(character => character == '*');
}
