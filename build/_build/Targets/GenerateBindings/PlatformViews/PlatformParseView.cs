namespace Build.Targets.GenerateBindings.PlatformViews;

public sealed record PlatformParseView(
    string Name,
    PlatformConditionKind Kind,
    string? SupportedOsPlatform,
    IReadOnlyList<string> Defines,
    IReadOnlyList<string> Undefines);
