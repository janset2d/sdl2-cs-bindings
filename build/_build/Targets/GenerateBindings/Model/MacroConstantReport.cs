namespace Build.Targets.GenerateBindings.Model;

public sealed record MacroConstantReport(
    int ParsedCount,
    int CandidateCount,
    int EmittedCount,
    int SkippedCount,
    int ExcludedCount,
    int OverriddenCount,
    int DuplicateCoalescedCount,
    int HelperCandidateCount,
    int HelperDuplicateCoalescedCount,
    int UnsupportedCount,
    int ConflictCount,
    IReadOnlyList<MacroConstantReportEntry> Entries)
{
    public static MacroConstantReport Empty { get; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, []);
}

public sealed record MacroConstantReportEntry(
    string Name,
    string SourceHeader,
    string ParseViewName,
    string Disposition,
    string Reason,
    string? EmittedType,
    string? EmittedValue,
    string MacroForm,
    string Taxonomy,
    string? OriginalExpression,
    string? ComputedValue);
