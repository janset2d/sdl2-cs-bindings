using Cake.Core.IO;

namespace Build.Features.Packaging;

public sealed record PackageArtifacts(FilePath ManagedPackage, FilePath ManagedSymbolsPackage, FilePath NativePackage);
