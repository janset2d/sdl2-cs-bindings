namespace Build.Targets.GenerateBindings.Model;

public sealed record NativeTypeDiagnostic(
    NativeTypeDiagnosticSeverity Severity,
    string Message,
    string? SourceHeader,
    string? NativeName);
