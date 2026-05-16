namespace Build.Targets.GenerateBindings.Model;

internal sealed record PreviewParseView(
    string Name,
    string? SupportedOsPlatform,
    IReadOnlyList<PreviewFunction> Functions);
