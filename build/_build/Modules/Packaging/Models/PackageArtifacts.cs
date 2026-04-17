using Cake.Core.IO;

namespace Build.Modules.Packaging.Models;

public sealed record PackageArtifacts(FilePath ManagedPackage, FilePath ManagedSymbolsPackage, FilePath NativePackage);
