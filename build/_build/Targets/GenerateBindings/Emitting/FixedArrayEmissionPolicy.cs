using Build.Targets.GenerateBindings.Model;

namespace Build.Targets.GenerateBindings.Emitting;

internal static class FixedArrayEmissionPolicy
{
    private static readonly HashSet<string> FixedBufferElementTypes = new(StringComparer.Ordinal)
    {
        "bool",
        "byte",
        "short",
        "int",
        "long",
        "char",
        "sbyte",
        "ushort",
        "uint",
        "ulong",
        "float",
        "double",
    };

    public static bool CanEmitFixedBuffer(BindingStructField field) =>
        field.FixedBufferLength is not null &&
        FixedBufferElementTypes.Contains(field.Type.ManagedName);
}
