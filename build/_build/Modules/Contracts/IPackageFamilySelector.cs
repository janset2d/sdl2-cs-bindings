using Build.Modules.Packaging.Results;

namespace Build.Modules.Contracts;

public interface IPackageFamilySelector
{
    /// <summary>
    /// Resolves the requested family list (or the default "all concrete families" when empty)
    /// into a topologically ordered <see cref="PackageFamilySelectionResult"/>. Unknown family
    /// names, placeholder families, empty selection, and dependency cycles return typed errors
    /// instead of throwing.
    /// </summary>
    PackageFamilySelectionResult Select(IReadOnlyList<string> requestedFamilies);
}
