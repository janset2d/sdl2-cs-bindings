namespace Build.Targets.GenerateBindings.Model;

public sealed record BindingParseView(
    string Name,
    string PlatformConditionKind,
    string? SupportedOsPlatform,
    IReadOnlyList<string> Defines,
    IReadOnlyList<string> Undefines,
    IReadOnlyList<BindingFunction> Functions)
{
    public BindingParseView(string Name, string? SupportedOsPlatform, IReadOnlyList<BindingFunction> Functions)
        : this(
            Name,
            SupportedOsPlatform is null ? "Neutral" : "OperatingSystem",
            SupportedOsPlatform,
            [],
            [],
            Functions)
    {
    }
}
