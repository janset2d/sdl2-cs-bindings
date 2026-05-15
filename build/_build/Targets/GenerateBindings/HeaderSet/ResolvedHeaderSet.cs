using Cake.Core.IO;

namespace Build.Targets.GenerateBindings.HeaderSet;

internal sealed record ResolvedHeaderSet(
    DirectoryPath IncludeRoot,
    DirectoryPath Sdl2IncludeDirectory,
    IReadOnlyList<FilePath> Headers);
