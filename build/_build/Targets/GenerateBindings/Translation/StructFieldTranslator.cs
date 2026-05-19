using System.Runtime.InteropServices;
using Build.Targets.GenerateBindings.Model;
using CppAst;

namespace Build.Targets.GenerateBindings.Translation;

internal sealed class StructFieldTranslator
{
    private readonly NativeTypeClassifier _typeClassifier;

    public StructFieldTranslator(NativeTypeClassifier typeClassifier)
    {
        _typeClassifier = typeClassifier ?? throw new ArgumentNullException(nameof(typeClassifier));
    }

    public BindingStructField Translate(CppField field, LayoutKind layout) =>
        Translate(field, parentStructName: null, layout, addNestedStruct: null, sourceHeader: null);

    public BindingStructField Translate(
        CppField field,
        string? parentStructName,
        LayoutKind layout,
        Action<CppClass, string>? addNestedStruct,
        string? sourceHeader = null)
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
                Type: NativeTypeRef.ConcreteStruct(generatedTypeName, generatedTypeName, null, sourceHeader),
                FieldOffset: layout == LayoutKind.Explicit ? checked((int)field.Offset) : null,
                FixedBufferLength: null);
        }

        return new BindingStructField(
            Name: TypeMappingPolicy.SafeIdentifier(field.Name),
            Type: _typeClassifier.Classify(fieldType, sourceHeader),
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
