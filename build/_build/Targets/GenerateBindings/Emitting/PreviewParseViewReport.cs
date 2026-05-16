namespace Build.Targets.GenerateBindings.Emitting;

internal sealed record PreviewParseViewReport(IReadOnlyList<PreviewParseViewReportEntry> Views);

internal sealed record PreviewParseViewReportEntry(
    string Name,
    string? SupportedOSPlatform,
    int FunctionCount,
    IReadOnlyList<PreviewParseViewReportFunction> Functions);

internal sealed record PreviewParseViewReportFunction(
    string Name,
    string SourceHeader);
