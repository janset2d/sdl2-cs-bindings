internal static class CSharpEvidenceLoader
{
    public static SourceEvidence LoadPath(string repoRoot, string relativePath)
    {
        var path = Path.Combine(repoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(path))
        {
            return Load(path, File.ReadAllText(path));
        }

        if (Directory.Exists(path))
        {
            var evidence = new CSharpEvidence([], [], []);
            foreach (var file in Directory.EnumerateFiles(path, "*.cs", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
            {
                evidence = Combine(evidence, CSharpEvidenceExtractor.Extract(file, File.ReadAllText(file), ["NET5_0_OR_GREATER"]));
            }

            return new SourceEvidence(SourceStatus.Present, evidence);
        }

        return new SourceEvidence(SourceStatus.Missing, new CSharpEvidence([], [], []));
    }

    public static SourceEvidence Load(string path, string source)
        => new(SourceStatus.Present, CSharpEvidenceExtractor.Extract(path, source, ["NET5_0_OR_GREATER"]));

    public static CSharpEvidence Combine(CSharpEvidence left, CSharpEvidence right)
        => new(
            left.Functions.Concat(right.Functions).ToArray(),
            left.Constants.Concat(right.Constants).ToArray(),
            left.Types.Concat(right.Types).ToArray());
}
