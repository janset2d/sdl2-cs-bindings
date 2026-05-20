using Build.Data.BindingGeneration.Models;
using Build.Targets.GenerateBindings.Model;

namespace Build.Targets.GenerateBindings.ModelBuilding.Macros;

internal static class RequiredConstantTranslator
{
    public static IReadOnlyList<BindingConstant> Translate(IReadOnlyList<RequiredConstantConfig> requiredConstants)
    {
        ArgumentNullException.ThrowIfNull(requiredConstants);

        return [.. requiredConstants.Select(required => new BindingConstant(
            required.Name,
            FromRequiredConstant(required),
            required.Value,
            required.Kind))];
    }

    private static NativeTypeRef FromRequiredConstant(RequiredConstantConfig required) => required.Type switch
    {
        "uint" => NativeTypeRef.Primitive("unsigned int", "uint", NativeAbiShape.Of("uint", 4), required.SourceHeader),
        "int" => NativeTypeRef.Primitive("int", "int", NativeAbiShape.Of("int", 4), required.SourceHeader),
        "ReadOnlySpan<byte>" => new NativeTypeRef(
            "const char[]", "ReadOnlySpan<byte>", NativeTypeKind.SubstitutedManagedType, 0,
            null, required.SourceHeader, NativeAbiShape.Of("ReadOnlySpan<byte>", IntPtr.Size * 2, isBlittable: false), null, []),
        _ => NativeTypeRef.Primitive(required.Type, required.Type, NativeAbiShape.Of(required.Type), required.SourceHeader),
    };
}
