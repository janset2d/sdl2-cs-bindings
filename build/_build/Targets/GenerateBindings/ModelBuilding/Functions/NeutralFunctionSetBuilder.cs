using Build.Targets.GenerateBindings.Model;
using Build.Targets.GenerateBindings.Parse;

namespace Build.Targets.GenerateBindings.ModelBuilding.Functions;

internal sealed class NeutralFunctionSetBuilder
{
    private readonly BindingFunctionTranslator _functionTranslator;

    public NeutralFunctionSetBuilder(BindingFunctionTranslator functionTranslator)
    {
        _functionTranslator = functionTranslator ?? throw new ArgumentNullException(nameof(functionTranslator));
    }

    public HashSet<string> Build(
        IReadOnlyList<CppAstParseResult> parseResults,
        IReadOnlyList<BindingFunction> requiredFunctions)
    {
        ArgumentNullException.ThrowIfNull(parseResults);
        ArgumentNullException.ThrowIfNull(requiredFunctions);

        var neutral = parseResults.FirstOrDefault(result =>
            string.Equals(result.ParseView.Name, "Neutral", StringComparison.Ordinal));
        var names = neutral is null
            ? new HashSet<string>(StringComparer.Ordinal)
            : _functionTranslator.Extract(neutral.Compilations)
                .Select(function => function.Name)
                .ToHashSet(StringComparer.Ordinal);

        foreach (var required in requiredFunctions)
        {
            names.Add(required.Name);
        }

        return names;
    }
}
