namespace Build.Targets.GenerateBindings.Emit;

public sealed record GeneratedFileSet(IReadOnlyList<GeneratedFile> Files);

public sealed record GeneratedFile(string RelativePath, string Content);
