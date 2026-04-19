using Cake.Core.IO;

namespace Build.Domain.Packaging.Models;

public sealed record PackageArtifacts(FilePath ManagedPackage, FilePath ManagedSymbolsPackage, FilePath NativePackage);
