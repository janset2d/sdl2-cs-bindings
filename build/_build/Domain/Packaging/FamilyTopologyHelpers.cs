using Build.Context.Models;

namespace Build.Domain.Packaging;

/// <summary>
/// Topologically orders a selected set of <see cref="PackageFamilyConfig"/> entries by
/// their <c>depends_on</c> relations so consumers (Pack, SetupLocalDev) invoke families
/// in dependency-safe order. Consistent with ADR-003 §2.5 ("<c>depends_on</c> does not
/// auto-expand scope"): deps pointing outside the selected set are ignored rather than
/// pulled in. Cycle detection surfaces an actionable error message instead of looping
/// silently.
/// </summary>
public static class FamilyTopologyHelpers
{
    public static bool TryOrderByDependencies(
        IReadOnlyList<PackageFamilyConfig> selected,
        out IReadOnlyList<PackageFamilyConfig> ordered,
        out string errorMessage)
    {
        ArgumentNullException.ThrowIfNull(selected);

        var selectedNames = selected
            .Select(family => family.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Per-family in-degree counts only deps inside the selected set; out-of-scope
        // dependencies are ignored per ADR-003 §2.5. A satellite family can therefore
        // ship without packing its core in the same invocation.
        var remainingInDegree = selected.ToDictionary(
            family => family.Name,
            family => family.DependsOn.Count(dep => selectedNames.Contains(dep)),
            StringComparer.OrdinalIgnoreCase);

        // Ties break alphabetically by family name so repeated invocations with the same
        // selection produce identical ordering — stabilises CI log diffs and license
        // payload layout for bit-reproducible pack outputs.
        var ready = new PriorityQueue<PackageFamilyConfig, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var family in selected.Where(family => remainingInDegree[family.Name] == 0))
        {
            ready.Enqueue(family, family.Name);
        }

        var result = new List<PackageFamilyConfig>(selected.Count);
        while (ready.Count > 0)
        {
            var current = ready.Dequeue();
            result.Add(current);

            foreach (var candidate in selected)
            {
                if (!candidate.DependsOn.Any(dep => string.Equals(dep, current.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                remainingInDegree[candidate.Name]--;
                if (remainingInDegree[candidate.Name] == 0)
                {
                    ready.Enqueue(candidate, candidate.Name);
                }
            }
        }

        if (result.Count != selected.Count)
        {
            var unordered = selected
                .Where(family => !result.Any(r => string.Equals(r.Name, family.Name, StringComparison.OrdinalIgnoreCase)))
                .Select(family => family.Name)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase);

            ordered = [];
            errorMessage =
                $"Cannot topologically order selected families: dependency cycle among [{string.Join(", ", unordered)}]. " +
                "Inspect manifest.json package_families[].depends_on for the cycle.";
            return false;
        }

        ordered = result;
        errorMessage = string.Empty;
        return true;
    }
}
