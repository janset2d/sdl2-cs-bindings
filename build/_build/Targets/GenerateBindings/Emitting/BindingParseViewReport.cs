namespace Build.Targets.GenerateBindings.Emitting;

internal sealed record BindingParseViewReport(
    int SchemaVersion,
    BindingParseViewReportCategories Categories,
    IReadOnlyList<string> EmittedFiles,
    IReadOnlyList<BindingParseViewReportEntry> Views,
    BindingMacroConstantsReport MacroConstants);

internal sealed record BindingMacroConstantsReport(
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
    IReadOnlyList<BindingMacroConstantReportEntry> Entries);

internal sealed record BindingMacroConstantReportEntry(
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

internal sealed record BindingParseViewReportCategories(
    int ViewCount,
    int FunctionCount,
    int StructCount,
    int EnumCount,
    int ConstantCount,
    int HandleCount,
    int CallbackCount,
    IReadOnlyList<string> Structs,
    IReadOnlyList<string> Enums,
    IReadOnlyList<string> Constants,
    IReadOnlyList<string> Handles,
    IReadOnlyList<string> Callbacks);

internal sealed record BindingParseViewReportEntry(
    string Name,
    string PlatformConditionKind,
    string? SupportedOSPlatform,
    IReadOnlyList<string> Defines,
    IReadOnlyList<string> Undefines,
    int FunctionCount,
    IReadOnlyList<BindingParseViewReportFunction> Functions);

internal sealed record BindingParseViewReportFunction(
    string Name,
    string SourceHeader,
    string ReturnType,
    IReadOnlyList<BindingParseViewReportParameter> Parameters);

internal sealed record BindingParseViewReportParameter(string Name, string Type);
