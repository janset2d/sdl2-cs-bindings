using Build.Context.Models;

namespace Build.Domain.Packaging.Models;

/// <summary>
/// Ordered list of families selected by <c>IPackageFamilySelector</c>. Ordering is topological
/// over <see cref="PackageFamilyConfig.DependsOn"/> so cross-family packs respect dependency order.
/// </summary>
public sealed record PackageFamilySelection(IReadOnlyList<PackageFamilyConfig> Families);
