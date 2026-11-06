using NuGet.Versioning;

namespace Build.Data.Versions;
/// <summary>
/// Iteration unit for <see cref="PackageFamilyVersionSet"/>. Pairs a package family
/// identifier with its NuGet version.
/// </summary>
public readonly record struct PackageFamilyVersion(PackageFamilyId Family, NuGetVersion Version);
