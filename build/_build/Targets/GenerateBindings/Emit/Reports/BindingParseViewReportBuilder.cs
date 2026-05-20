using Build.Targets.GenerateBindings.Model;

namespace Build.Targets.GenerateBindings.Emit.Reports;

internal static class BindingParseViewReportBuilder
{
    public static BindingParseViewReport Build(BindingModel model, IReadOnlyList<string> emittedFiles)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(emittedFiles);

        return new BindingParseViewReport(
            SchemaVersion: 1,
            Categories: BuildCategories(model),
            EmittedFiles: emittedFiles,
            Views: BuildViewEntries(model),
            MacroConstants: BuildMacroConstants(model));
    }

    private static BindingParseViewReportCategories BuildCategories(BindingModel model) =>
        new(
            ViewCount: model.Views.Count,
            FunctionCount: model.Views.Sum(view => view.Functions.Count),
            StructCount: model.Structs.Count,
            EnumCount: model.Enums.Count,
            ConstantCount: model.Constants.Count,
            HandleCount: model.Handles.Count,
            CallbackCount: model.Callbacks.Count,
            Structs: [.. model.Structs.Select(s => s.Name)],
            Enums: [.. model.Enums.Select(e => e.Name)],
            Constants: [.. model.Constants.Select(c => c.Name)],
            Handles: [.. model.Handles.Select(h => h.Name)],
            Callbacks: [.. model.Callbacks.Select(c => c.Name)]);

    private static List<BindingParseViewReportEntry> BuildViewEntries(BindingModel model) =>
        model.Views.Select(view =>
            new BindingParseViewReportEntry(
                Name: view.Name,
                PlatformConditionKind: view.PlatformConditionKind,
                SupportedOSPlatform: view.SupportedOsPlatform,
                Defines: view.Defines,
                Undefines: view.Undefines,
                FunctionCount: view.Functions.Count,
                Functions: [.. view.Functions.Select(f => new BindingParseViewReportFunction(
                    f.Name,
                    f.SourceHeader,
                    f.ReturnType.ManagedName,
                    [.. f.Parameters.Select(p => new BindingParseViewReportParameter(p.Name, p.Type.ManagedName))]))])).ToList();

    private static BindingMacroConstantsReport BuildMacroConstants(BindingModel model) =>
        new(
            model.MacroReport.ParsedCount,
            model.MacroReport.CandidateCount,
            model.MacroReport.EmittedCount,
            model.MacroReport.SkippedCount,
            model.MacroReport.ExcludedCount,
            model.MacroReport.OverriddenCount,
            model.MacroReport.DuplicateCoalescedCount,
            model.MacroReport.HelperCandidateCount,
            model.MacroReport.HelperDuplicateCoalescedCount,
            model.MacroReport.UnsupportedCount,
            model.MacroReport.ConflictCount,
            [.. model.MacroReport.Entries.Select(entry => new BindingMacroConstantReportEntry(
                entry.Name,
                entry.SourceHeader,
                entry.ParseViewName,
                entry.Disposition,
                entry.Reason,
                entry.EmittedType,
                entry.EmittedValue,
                entry.MacroForm,
                entry.Taxonomy,
                entry.OriginalExpression,
                entry.ComputedValue))]);
}
