namespace Build.Targets.GenerateBindings.Parsing;

internal sealed record PlatformParseView(
    string Name,
    PlatformConditionKind Kind,
    string? SupportedOsPlatform,
    IReadOnlyList<string> Defines,
    IReadOnlyList<string> Undefines);
