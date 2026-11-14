namespace Build.Targets.GenerateBindings.Oracle;

internal sealed record FunctionEvidence(
    string ManagedName,
    string NativeEntryPoint,
    string NativeLibrary,
    string ImportKind,
    string Accessibility,
    string ContainingType,
    string ContainingTypePath,
    IReadOnlyList<string> ContainingTypeAccessibilities,
    string NamespaceName,
    string SourcePath,
    string ReturnType,
    string ReturnNativeTypeName,
    IReadOnlyList<ParameterEvidence> Parameters)
{
    public bool IsNativeImportDeclaration { get; init; }
}
