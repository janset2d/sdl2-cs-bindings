using Cake.Core.IO;

namespace Build.Targets.GenerateBindings;

internal sealed record GenerateBindingsRequest(
    Sdl2CoreGenerationConfig Config,
    DirectoryPath VcpkgInstalledDirectory,
    string Triplet,
    DirectoryPath OutputDirectory);
