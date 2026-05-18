using Build.Data.BindingGeneration.Models;
using Build.Targets.GenerateBindings.Model;
using Build.Targets.GenerateBindings.Parsing;

namespace Build.Targets.GenerateBindings.Translation;

/// <summary>
/// Coordinates the CppAst parse-result translation step into the binding model.
/// Parse views stay in input order, required functions are injected into Neutral,
/// and platform views subtract the Neutral symbol set.
/// </summary>
internal static class CppAstToBindingModel
{
    public static BindingModel Translate(
        IReadOnlyList<CppAstParseResult> parseResults,
        BindingGenerationConfig config,
        IReadOnlyList<BindingFunction> requiredFunctions)
    {
        ArgumentNullException.ThrowIfNull(parseResults);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(requiredFunctions);

        var unsupportedPolicy = new KnownUnsupportedDeclarationPolicy(config);
        var declarationPolicy = new BindableDeclarationPolicy(config, unsupportedPolicy);
        var functionTranslator = new BindingFunctionTranslator(declarationPolicy);
        var functionDeduplicator = new BindingFunctionDeduplicator();
        var neutralFunctionNames = new NeutralFunctionSetBuilder(functionTranslator)
            .Build(parseResults, requiredFunctions);
        var structs = new BindingStructTranslator(declarationPolicy)
            .Extract(parseResults);
        var constants = RequiredConstantTranslator.Translate(config.RequiredConstants);

        var views = new List<BindingParseView>(parseResults.Count);
        foreach (var result in parseResults)
        {
            var functions = functionTranslator.Extract(result.Compilations);
            var isNeutral = string.Equals(result.ParseView.Name, "Neutral", StringComparison.Ordinal);

            if (isNeutral)
            {
                // Required functions are hand-curated declarations that the per-header parse loop
                // cannot reach; render them first so base lifecycle calls stay at the top.
                functions = [.. requiredFunctions, .. functions];
                functionDeduplicator.AddRange(functions);
            }
            else
            {
                // Platform views only emit symbols that are absent from Neutral, including
                // required functions injected into Neutral outside the parsed header set.
                functions = functions
                    .Where(function => !neutralFunctionNames.Contains(function.Name))
                    .ToList();
                functions = functionDeduplicator.ExcludeAlreadyEmitted(functions);
            }

            views.Add(new BindingParseView(
                Name: result.ParseView.Name,
                SupportedOsPlatform: result.ParseView.SupportedOsPlatform,
                Functions: functions));
        }

        return new BindingModel(views, structs, [], constants, [], []);
    }
}
