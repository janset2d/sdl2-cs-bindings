using Cake.Core.IO;

namespace Build.Features.Harvesting;

public sealed record PackageInfo(string PackageName, string Triplet, IReadOnlyList<FilePath> OwnedFiles, IReadOnlyList<string> DeclaredDependencies);
