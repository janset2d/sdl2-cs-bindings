namespace Build.Modules.Vcpkg.Models;

using System.Collections.Generic;
using Cake.Core.IO;

public sealed record PackageInfo(
    string PackageName, // e.g., "sdl2", "libwebp"
    string Triplet,  // e.g., "x64-windows-release"
    IReadOnlyList<FilePath> OwnedFiles, // All files owned by the package from vcpkg output
    IReadOnlyList<string> DeclaredDependencies // Names of dependent vcpkg packages (e.g., "libpng:x64-windows-release")
);
