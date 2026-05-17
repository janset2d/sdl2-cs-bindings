namespace Build.Targets.GenerateBindings.Model;

public sealed record PreviewFunction(
    string Name,
    string ReturnType,
    IReadOnlyList<PreviewParameter> Parameters,
    string SourceHeader);
