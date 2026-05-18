using System.Runtime.InteropServices;
using Build.Targets.GenerateBindings.Model;
using CppAst;

namespace Build.Targets.GenerateBindings.Translation;

internal static class StructFieldTranslator
{
    public static BindingStructField Translate(CppField field, LayoutKind layout) =>
        Translate(field, parentStructName: null, layout, addNestedStruct: null);

    public static BindingStructField Translate(
        CppField field,
        string? parentStructName,
        LayoutKind layout,
        Action<CppClass, string>? addNestedStruct)
    {
        ArgumentNullException.ThrowIfNull(field);

        var fieldType = field.Type;
        int? fixedBufferLength = null;
        if (fieldType is CppArrayType array)
        {
            fieldType = array.ElementType;
            fixedBufferLength = array.Size;
        }

        if (fieldType is CppClass { IsAnonymous: true } anonymousClass)
        {
            if (string.IsNullOrWhiteSpace(parentStructName) || addNestedStruct is null)
            {
                throw new InvalidOperationException(
                    $"Anonymous field '{field.Name}' requires a parent struct name and nested struct collector.");
            }

            var generatedTypeName = CreateAnonymousTypeName(parentStructName, field.Name);
            addNestedStruct(anonymousClass, generatedTypeName);
            return new BindingStructField(
                Name: TypeMappingPolicy.SafeIdentifier(field.Name),
                Type: BindingTypeRef.Of(generatedTypeName),
                FieldOffset: layout == LayoutKind.Explicit ? checked((int)field.Offset) : null,
                FixedBufferLength: null);
        }

        return new BindingStructField(
            Name: TypeMappingPolicy.SafeIdentifier(field.Name),
            Type: TypeMappingPolicy.Map(fieldType),
            FieldOffset: layout == LayoutKind.Explicit ? checked((int)field.Offset) : null,
            FixedBufferLength: fixedBufferLength);
    }

    private static string CreateAnonymousTypeName(string parentStructName, string fieldName)
    {
        var segment = string.IsNullOrWhiteSpace(fieldName)
            ? "Anonymous"
            : TypeMappingPolicy.SafeIdentifier(fieldName).TrimStart('@');
        return parentStructName + "_" + segment;
    }
}
