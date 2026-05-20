namespace Build.Targets.GenerateBindings.ModelBuilding.Macros;

internal sealed record MacroExpressionEvaluationContext(
    IReadOnlyDictionary<string, MacroIntegerExpressionValue> Constants,
    IReadOnlyDictionary<string, MacroFunctionLikeMacro> Functions);

internal sealed record MacroFunctionLikeMacro(
    string Name,
    IReadOnlyList<string> Parameters,
    string Body);
