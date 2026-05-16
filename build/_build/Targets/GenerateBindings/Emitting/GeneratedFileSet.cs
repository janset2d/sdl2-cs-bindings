namespace Build.Targets.GenerateBindings.Emitting;

internal sealed record GeneratedFileSet(IReadOnlyList<GeneratedFile> Files);

internal sealed record GeneratedFile(string RelativePath, string Content);
