namespace Build.Targets.GenerateBindings.Parsing;

public sealed record PlatformParseView(
    string Name,
    PlatformConditionKind Kind,
    string? SupportedOsPlatform,
    IReadOnlyList<string> Defines,
    IReadOnlyList<string> Undefines);
