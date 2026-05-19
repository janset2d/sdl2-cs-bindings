namespace Build.Targets.GenerateBindings.Emitting;

internal sealed record BindingParseViewReport(
    int SchemaVersion,
    BindingParseViewReportCategories Categories,
    IReadOnlyList<string> EmittedFiles,
    IReadOnlyList<BindingParseViewReportEntry> Views);

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
