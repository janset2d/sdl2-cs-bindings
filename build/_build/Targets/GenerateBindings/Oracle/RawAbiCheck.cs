namespace Build.Targets.GenerateBindings.Oracle;

internal sealed record RawAbiCheck(string CheckId, string Severity, string Classification, string Symbol, string Message, string SourcePath);
