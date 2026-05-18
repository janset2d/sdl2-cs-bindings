using Build.Targets.GenerateBindings.Model;
using CppAst;

namespace Build.Targets.GenerateBindings.Translation;

internal sealed class BindingFunctionTranslator
{
    private readonly BindableDeclarationPolicy _declarationPolicy;

    public BindingFunctionTranslator(BindableDeclarationPolicy declarationPolicy)
    {
        _declarationPolicy = declarationPolicy ?? throw new ArgumentNullException(nameof(declarationPolicy));
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

    private static BindingFunction Translate(CppFunction function)
    {
        var sourceHeader = Path.GetFileName(function.SourceFile ?? string.Empty);
        var parameters = function.Parameters
            .Select((parameter, index) => new BindingParameter(
                TypeMappingPolicy.Map(parameter.Type),
                TypeMappingPolicy.SafeIdentifier(parameter.Name, index)))
            .ToList();

        return new BindingFunction(
            Name: function.Name,
            ReturnType: TypeMappingPolicy.Map(function.ReturnType),
            Parameters: parameters,
            SourceHeader: sourceHeader);
    }
}
