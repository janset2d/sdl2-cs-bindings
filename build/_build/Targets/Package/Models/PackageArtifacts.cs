using Cake.Core.IO;

namespace Build.Targets.Package.Models;

public sealed record PackageArtifacts(FilePath ManagedPackage, FilePath ManagedSymbolsPackage, FilePath NativePackage);
