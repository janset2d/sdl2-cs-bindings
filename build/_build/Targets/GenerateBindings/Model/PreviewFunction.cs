namespace Build.Targets.GenerateBindings.Model;

internal sealed record PreviewFunction(
    string Name,
    string ReturnType,
    IReadOnlyList<PreviewParameter> Parameters,
    string SourceHeader);
