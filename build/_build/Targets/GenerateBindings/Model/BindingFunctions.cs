namespace Build.Targets.GenerateBindings.Model;

public sealed record BindingFunction(
    string Name,
    NativeTypeRef ReturnType,
    IReadOnlyList<BindingParameter> Parameters,
    string SourceHeader);

public sealed record BindingParameter(NativeTypeRef Type, string Name);
