namespace Build.Targets.GenerateBindings.Model;

public sealed record BindingParseView(
    string Name,
    string? SupportedOsPlatform,
    IReadOnlyList<BindingFunction> Functions);
