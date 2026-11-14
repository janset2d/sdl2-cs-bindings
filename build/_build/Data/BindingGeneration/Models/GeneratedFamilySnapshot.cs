using Cake.Core.IO;

namespace Build.Data.BindingGeneration.Models;

public sealed record GeneratedFamilySnapshot(DirectoryPath Root, DirectoryPath OutputDirectory, IReadOnlyList<GeneratedFile> Files);
