using Build.Data.BindingGeneration.Models;
using Build.Targets.GenerateBindings.Model;
using Build.Targets.GenerateBindings.ModelBuilding.Declarations;
using Build.Targets.GenerateBindings.ModelBuilding.Functions;
using Build.Targets.GenerateBindings.ModelBuilding.Macros;
using Build.Targets.GenerateBindings.ModelBuilding.Types;
using Build.Targets.GenerateBindings.Parse;

namespace Build.Targets.GenerateBindings.ModelBuilding;

/// <summary>
/// Coordinates the CppAst parse-result translation step into the binding model.
/// Parse views stay in input order, required functions are injected into Neutral,
/// and platform views subtract the Neutral symbol set.
/// </summary>
public sealed class BindingModelBuilder
{
    private readonly StringComparer _viewNameComparer = StringComparer.Ordinal;

    public BindingModel Build(
        IReadOnlyList<CppAstParseResult> parseResults,
        BindingGenerationConfig config,
        IReadOnlyList<BindingFunction> requiredFunctions)
    {
        ArgumentNullException.ThrowIfNull(parseResults);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(requiredFunctions);

        var unsupportedPolicy = new KnownUnsupportedDeclarationPolicy(config);
        var declarationPolicy = new BindableDeclarationPolicy(config, unsupportedPolicy);
        var declarationCatalog = new NativeDeclarationCatalogBuilder(declarationPolicy)
            .Build(parseResults);
        var classificationContext = NativeTypeClassificationContext.FromConfig(config);
        var typeClassifier = new NativeTypeClassifier(classificationContext);
        var functionTranslator = new BindingFunctionTranslator(declarationPolicy, typeClassifier);
        var functionDeduplicator = new BindingFunctionDeduplicator();
        var neutralFunctionNames = new NeutralFunctionSetBuilder(functionTranslator)
            .Build(parseResults, requiredFunctions);
        var structs = new BindingStructTranslator(declarationPolicy, typeClassifier)
            .Extract(declarationCatalog);
        var enums = new BindingEnumTranslator(declarationPolicy, typeClassifier)
            .Extract(declarationCatalog.Enums);
        var constantTranslation = BindingConstantTranslator.Translate(parseResults, config);
        var handles = new BindingHandleTranslator(declarationPolicy, typeClassifier)
            .Extract(declarationCatalog);
        var callbacks = new BindingCallbackTranslator(declarationPolicy, typeClassifier)
            .Extract(declarationCatalog.Typedefs);

        var views = new List<BindingParseView>(parseResults.Count);
        foreach (var result in parseResults)
        {
            var functions = functionTranslator.Extract(result.Compilations);
            var isNeutral = _viewNameComparer.Equals(result.ParseView.Name, "Neutral");

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
                PlatformConditionKind: result.ParseView.Kind.ToString(),
                SupportedOsPlatform: result.ParseView.SupportedOsPlatform,
                Defines: result.ParseView.Defines,
                Undefines: result.ParseView.Undefines,
                Functions: functions));
        }

        return new BindingModel(views, structs, enums, constantTranslation.Constants, handles, callbacks)
        {
            MacroReport = constantTranslation.Report,
        };
    }
}
