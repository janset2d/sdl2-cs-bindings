namespace Build.Targets.GenerateBindings.Oracle;

internal sealed record ConstantEvidence(string Name, string Kind, string ContainingType, string NamespaceName, string SourcePath)
{
    public string? Initializer { get; init; }
    public string ManagedType { get; init; } = "";
    public string NativeTypeName { get; init; } = "";
}
