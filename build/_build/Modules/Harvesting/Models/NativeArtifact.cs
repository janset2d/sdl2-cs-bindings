namespace Build.Modules.Harvesting.Models;

using Cake.Core.IO;

public enum ArtifactOrigin
{
    Primary,
    Runtime,
    Metadata,
    License
}

public sealed record NativeArtifact(
    string FileName,        // e.g., "SDL2.dll"
    FilePath SourcePath,    // Full path in vcpkg_installed
    FilePath TargetPath,    // Intended path in the output package/archive
    string PackageName,     // Vcpkg package name (e.g., "sdl2")
    ArtifactOrigin Origin
);