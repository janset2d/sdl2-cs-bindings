namespace Build.Targets.GenerateBindings.Emitting;

internal sealed record BindingParseViewReport(IReadOnlyList<BindingParseViewReportEntry> Views);

internal sealed record BindingParseViewReportEntry(
    string Name,
    string? SupportedOSPlatform,
    int FunctionCount,
    IReadOnlyList<BindingParseViewReportFunction> Functions);

internal sealed record BindingParseViewReportFunction(
    string Name,
    string SourceHeader);
