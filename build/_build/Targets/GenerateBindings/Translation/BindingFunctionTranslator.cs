using Build.Targets.GenerateBindings.Model;
using CppAst;

namespace Build.Targets.GenerateBindings.Translation;

internal sealed class BindingFunctionTranslator
{
    private readonly BindableDeclarationPolicy _declarationPolicy;
    private readonly NativeTypeClassifier _typeClassifier;

    public BindingFunctionTranslator(BindableDeclarationPolicy declarationPolicy, NativeTypeClassifier typeClassifier)
    {
        _declarationPolicy = declarationPolicy ?? throw new ArgumentNullException(nameof(declarationPolicy));
        _typeClassifier = typeClassifier ?? throw new ArgumentNullException(nameof(typeClassifier));
    }

    public List<BindingFunction> Extract(IReadOnlyList<CppCompilation> compilations)
    {
        ArgumentNullException.ThrowIfNull(compilations);

        var seen = new HashSet<(string SourceFile, string Name)>();
        var functions = new List<BindingFunction>();

        foreach (var compilation in compilations)
        {
            foreach (var function in compilation.Functions)
            {
                if (!_declarationPolicy.IsBindableFunction(function))
                {
                    continue;
                }

                var key = (function.SourceFile ?? string.Empty, function.Name);
                if (!seen.Add(key))
                {
                    continue;
                }

                functions.Add(Translate(function));
            }
        }

        return functions
            .OrderBy(function => function.SourceHeader, StringComparer.Ordinal)
            .ThenBy(function => function.Name, StringComparer.Ordinal)
            .ToList();
    }

    private BindingFunction Translate(CppFunction function)
    {
        var sourceHeader = Path.GetFileName(function.SourceFile ?? string.Empty);
        var parameters = function.Parameters
            .Select((parameter, index) => new BindingParameter(
                ClassifyParameter(function.Name, parameter, sourceHeader),
                TypeMappingPolicy.SafeIdentifier(parameter.Name, index)))
            .ToList();

        return new BindingFunction(
            Name: function.Name,
            ReturnType: _typeClassifier.Classify(function.ReturnType, sourceHeader),
            Parameters: parameters,
            SourceHeader: sourceHeader);
    }

    private NativeTypeRef ClassifyParameter(string functionName, CppParameter parameter, string? sourceHeader) =>
        SdlWideStringPointerPolicy.TryParameter(functionName, parameter.Name, parameter.Type, sourceHeader, out var widePointer)
            ? widePointer
            : _typeClassifier.Classify(parameter.Type, sourceHeader);
}
