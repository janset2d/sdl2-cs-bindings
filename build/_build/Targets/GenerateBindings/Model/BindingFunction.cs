namespace Build.Targets.GenerateBindings.Model;

public sealed record BindingFunction(
    string Name,
    BindingTypeRef ReturnType,
    IReadOnlyList<BindingParameter> Parameters,
    string SourceHeader);
