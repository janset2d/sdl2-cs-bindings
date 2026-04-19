using Cake.Core.IO;

namespace Build.Domain.Harvesting.Models;

public sealed record PackageInfo(string PackageName, string Triplet, IReadOnlyList<FilePath> OwnedFiles, IReadOnlyList<string> DeclaredDependencies);
