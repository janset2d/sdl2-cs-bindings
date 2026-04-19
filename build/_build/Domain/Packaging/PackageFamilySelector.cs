using Build.Context.Models;
using Build.Domain.Packaging.Models;
using Build.Domain.Packaging.Results;
using Cake.Core.Diagnostics;

namespace Build.Domain.Packaging;

public sealed class PackageFamilySelector(ManifestConfig manifestConfig, ICakeLog log) : IPackageFamilySelector
{
    private readonly ManifestConfig _manifestConfig = manifestConfig ?? throw new ArgumentNullException(nameof(manifestConfig));
    private readonly ICakeLog _log = log ?? throw new ArgumentNullException(nameof(log));

    public PackageFamilySelectionResult Select(IReadOnlyList<string> requestedFamilies)
    {
        ArgumentNullException.ThrowIfNull(requestedFamilies);

        var availableFamilies = _manifestConfig.PackageFamilies.ToList();
        var selected = new List<PackageFamilyConfig>();

        if (requestedFamilies.Count == 0)
        {
            foreach (var family in availableFamilies)
            {
                if (!HasConcreteProjects(family))
                {
                    _log.Information("Skipping family '{0}' because it does not yet declare both managed and native projects in manifest.json.", family.Name);
                    continue;
                }

                selected.Add(family);
            }
        }
        else
        {
            foreach (var requestedFamily in requestedFamilies.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var family = availableFamilies.SingleOrDefault(candidate => string.Equals(candidate.Name, requestedFamily, StringComparison.OrdinalIgnoreCase));
                if (family is null)
                {
                    return new PackageFamilySelectionError(
                        $"Package task received unknown family '{requestedFamily}'. Add it to build/manifest.json package_families[] or fix the CLI value.");
                }

                if (!HasConcreteProjects(family))
                {
                    return new PackageFamilySelectionError(
                        $"Package task cannot pack family '{family.Name}' yet because manifest.json does not declare both managed_project and native_project. This usually means the family is still a placeholder.");
                }

                selected.Add(family);
            }
        }

        if (selected.Count == 0)
        {
            return new PackageFamilySelectionError("Package task found no concrete families to pack.");
        }

        if (!TryTopologicallyOrderFamilies(selected, out var ordered, out var errorMessage))
        {
            return new PackageFamilySelectionError(errorMessage);
        }

        return new PackageFamilySelection(ordered);
    }

    private static bool HasConcreteProjects(PackageFamilyConfig family)
    {
        return !string.IsNullOrWhiteSpace(family.ManagedProject) && !string.IsNullOrWhiteSpace(family.NativeProject);
    }

    private static bool TryTopologicallyOrderFamilies(List<PackageFamilyConfig> families, out IReadOnlyList<PackageFamilyConfig> ordered, out string errorMessage)
    {
        var remaining = families.ToList();
        var processed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var orderedList = new List<PackageFamilyConfig>(families.Count);

        while (orderedList.Count < families.Count)
        {
            var next = remaining.FirstOrDefault(family =>
                family.DependsOn.All(processed.Contains) ||
                family.DependsOn.All(dep => families.TrueForAll(candidate => !string.Equals(candidate.Name, dep, StringComparison.OrdinalIgnoreCase))));

            if (next is null)
            {
                ordered = [];
                errorMessage = "Package task could not order selected families because a dependency cycle or unresolved dependency was detected.";
                return false;
            }

            orderedList.Add(next);
            processed.Add(next.Name);
            remaining.Remove(next);
        }

        ordered = orderedList;
        errorMessage = string.Empty;
        return true;
    }
}
