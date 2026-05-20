using Build.Data.BindingGeneration.Models;
using Build.Targets.GenerateBindings.Model;

namespace Build.Targets.GenerateBindings.ModelBuilding.Macros;

internal sealed record MacroManualPolicyResult(
    IReadOnlyList<BindingConstant> Constants,
    IReadOnlyList<MacroConstantReportEntry> Entries,
    int ExcludedCount,
    int OverriddenCount);

internal static class MacroManualPolicyApplier
{
    public static MacroManualPolicyResult Apply(
        IReadOnlyList<BindingConstant> generatedConstants,
        IReadOnlyList<MacroConstantReportEntry> entries,
        BindingGenerationConfig config)
    {
        ArgumentNullException.ThrowIfNull(generatedConstants);
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(config);

        var constants = generatedConstants.ToDictionary(c => c.Name, StringComparer.Ordinal);
        var reportEntries = entries.ToList();
        var excludedCount = 0;
        var overriddenCount = 0;

        foreach (var required in config.RequiredConstants)
        {
            if (constants.ContainsKey(required.Name))
            {
                if (!required.AllowStale)
                    throw new InvalidOperationException($"Manual required constant '{required.Name}' is redundant because the macro is generated from headers.");
                reportEntries.Add(ManualEntry(required.Name, required.SourceHeader, "stale-include", required.Reason ?? "manual required constant kept as stale-tolerant", null, null));
                continue;
            }

            var translated = RequiredConstantTranslator.Translate([required]).Single();
            constants.Add(translated.Name, translated);
            reportEntries.Add(ManualEntry(required.Name, required.SourceHeader, "included", required.Reason ?? "manual required constant", translated.Type.ManagedName, translated.Value));
        }

        foreach (var exclusion in config.MacroConstants.Excluded)
        {
            if (!constants.Remove(exclusion.Key))
            {
                if (!exclusion.Value.AllowStale)
                    throw new InvalidOperationException($"Manual macro exclusion '{exclusion.Key}' was not consumed.");
                reportEntries.Add(ManualEntry(exclusion.Key, "manifest", "stale-exclude", exclusion.Value.Reason, null, null));
                continue;
            }

            excludedCount++;
            reportEntries.Add(ManualEntry(exclusion.Key, "manifest", "excluded", exclusion.Value.Reason, null, null));
        }

        foreach (var overrideEntry in config.MacroConstants.Overrides)
        {
            if (!constants.ContainsKey(overrideEntry.Key))
            {
                if (!overrideEntry.Value.AllowStale)
                    throw new InvalidOperationException($"Manual macro override '{overrideEntry.Key}' was not consumed.");
                reportEntries.Add(ManualEntry(overrideEntry.Key, overrideEntry.Value.SourceHeader, "stale-override", overrideEntry.Value.Reason, null, null));
                continue;
            }

            var replacementConfig = new RequiredConstantConfig
            {
                Name = overrideEntry.Key,
                Type = overrideEntry.Value.Type,
                Value = overrideEntry.Value.Value,
                SourceHeader = overrideEntry.Value.SourceHeader,
                Kind = overrideEntry.Value.Kind,
                Reason = overrideEntry.Value.Reason,
                AllowStale = overrideEntry.Value.AllowStale,
            };
            var replacement = RequiredConstantTranslator.Translate([replacementConfig]).Single();
            constants[overrideEntry.Key] = replacement;
            overriddenCount++;
            reportEntries.Add(ManualEntry(overrideEntry.Key, overrideEntry.Value.SourceHeader, "overridden", overrideEntry.Value.Reason, replacement.Type.ManagedName, replacement.Value));
        }

        return new MacroManualPolicyResult(
            constants.Values.OrderBy(c => c.Name, StringComparer.Ordinal).ToList(),
            reportEntries,
            excludedCount,
            overriddenCount);
    }

    private static MacroConstantReportEntry ManualEntry(
        string name,
        string sourceHeader,
        string disposition,
        string reason,
        string? emittedType,
        string? emittedValue) =>
        new(
            name,
            sourceHeader,
            "manual",
            disposition,
            reason,
            emittedType,
            emittedValue,
            "manual",
            "manual-policy",
            null,
            null);
}
