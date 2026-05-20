using Build.Targets.GenerateBindings.Model;
using Build.Targets.GenerateBindings.ModelBuilding.Types;
using CppAst;

namespace Build.Targets.GenerateBindings.ModelBuilding.Declarations;

internal sealed class BindingCallbackTranslator
{
    private readonly BindableDeclarationPolicy _declarationPolicy;
    private readonly NativeTypeClassifier _typeClassifier;

    public BindingCallbackTranslator(BindableDeclarationPolicy declarationPolicy, NativeTypeClassifier typeClassifier)
    {
        _declarationPolicy = declarationPolicy ?? throw new ArgumentNullException(nameof(declarationPolicy));
        _typeClassifier = typeClassifier ?? throw new ArgumentNullException(nameof(typeClassifier));
    }

    public IReadOnlyList<BindingCallback> Extract(IReadOnlyList<CppTypedef> typedefs)
    {
        ArgumentNullException.ThrowIfNull(typedefs);

        return typedefs
            .Where(IsBindableCallbackTypedef)
            .Select(TryTranslate)
            .Where(callback => callback is not null)
            .OrderBy(callback => callback!.Name, StringComparer.Ordinal)
            .Select(callback => callback!)
            .ToList();
    }

    private bool IsBindableCallbackTypedef(CppTypedef typedef) =>
        _declarationPolicy.IsBindableOwnedType(typedef);

    private BindingCallback? TryTranslate(CppTypedef typedef)
    {
        if (UnwrapQualified(typedef.ElementType) is not CppPointerType pointer ||
            UnwrapQualified(pointer.ElementType) is not CppFunctionType functionType)
        {
            return null;
        }

        var sourceHeader = typedef.SourceFile.ToSourceHeaderName();
        var parameters = functionType.Parameters
            .Select((parameter, index) => new BindingParameter(
                _typeClassifier.Classify(parameter.Type, sourceHeader),
                TypeMappingPolicy.SafeIdentifier(parameter.Name, index)))
            .ToList();

        return new BindingCallback(
            typedef.Name,
            _typeClassifier.Classify(functionType.ReturnType, sourceHeader),
            parameters);
    }

    private static CppType UnwrapQualified(CppType type)
    {
        while (type is CppQualifiedType qualified)
            type = qualified.ElementType;

        return type;
    }
}
