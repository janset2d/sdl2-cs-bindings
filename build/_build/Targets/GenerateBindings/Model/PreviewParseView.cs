namespace Build.Targets.GenerateBindings.Model;

public sealed record PreviewParseView(
    string Name,
    string? SupportedOsPlatform,
    IReadOnlyList<PreviewFunction> Functions);
