using Cake.Core.IO;

namespace Build.Targets.GenerateBindings.HeaderSet;

public sealed record ResolvedHeaderSet(
    DirectoryPath IncludeRoot,
    DirectoryPath SyntheticIncludeRoot,
    DirectoryPath FamilyIncludeDirectory,
    IReadOnlyList<FilePath> Headers);
