using Build.Targets.GenerateBindings.Model;

namespace Build.Targets.GenerateBindings.ModelBuilding.Macros;

internal sealed record MacroConstantMergeResult(
    IReadOnlyList<BindingConstant> Constants,
    IReadOnlyList<MacroConstantReportEntry> Entries,
    int DuplicateCoalescedCount);

internal static class MacroConstantMerger
{
    public static MacroConstantMergeResult Merge(IReadOnlyList<MacroValueClassification> classifications)
    {
        ArgumentNullException.ThrowIfNull(classifications);

        var constants = new List<BindingConstant>();
        var entries = new List<MacroConstantReportEntry>();
        var byName = new Dictionary<string, BindingConstant>(StringComparer.Ordinal);
        var coalesced = 0;

        foreach (var classification in classifications)
        {
            entries.Add(classification.Report);
            if (classification.Constant is null)
                continue;

            if (byName.TryGetValue(classification.Constant.Name, out var existing))
            {
                if (!IsCompatible(existing, classification.Constant))
                {
                    throw new InvalidOperationException(
                        $"Incompatible macro constant definitions for '{classification.Constant.Name}'. Existing value '{existing.Value}'/{existing.Type.ManagedName}; new value '{classification.Constant.Value}'/{classification.Constant.Type.ManagedName}.");
                }

                coalesced++;
                continue;
            }

            byName.Add(classification.Constant.Name, classification.Constant);
            constants.Add(classification.Constant);
        }

        return new MacroConstantMergeResult(
            constants.OrderBy(c => c.Name, StringComparer.Ordinal).ToList(),
            entries,
            coalesced);
    }

    private static bool IsCompatible(BindingConstant left, BindingConstant right) =>
        string.Equals(left.Name, right.Name, StringComparison.Ordinal)
        && string.Equals(left.Type.ManagedName, right.Type.ManagedName, StringComparison.Ordinal)
        && string.Equals(left.Value, right.Value, StringComparison.Ordinal)
        && left.Kind == right.Kind;
}
