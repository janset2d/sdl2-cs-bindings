using Build.Host.Cake;
using Cake.Core;
using Cake.Core.IO;

namespace Build.Targets.GenerateBindings.Oracle;

internal static class CSharpEvidenceLoader
{
    public static async Task<SourceEvidence> LoadPathAsync(ICakeContext context, DirectoryPath repoRoot, string relativePath)
    {
        var path = repoRoot.CombineWithFilePath(relativePath);

        if (context.FileSystem.GetFile(path).Exists)
        {
            return Load(path.FullPath, await context.ReadAllTextAsync(path));
        }

        var directory = context.FileSystem.GetDirectory(repoRoot.Combine(relativePath));

        if (directory.Exists)
        {
            var evidence = new CSharpEvidence([], [], []);

            foreach (var file in directory.GetFiles("*.cs", SearchScope.Recursive).Select(file => file.Path).OrderBy(path => path.FullPath, StringComparer.Ordinal))
            {
                evidence = Combine(evidence, Load(file.FullPath, await context.ReadAllTextAsync(file)).Evidence);
            }

            return new SourceEvidence(SourceStatus.Present, evidence);
        }

        return new SourceEvidence(SourceStatus.Missing, new CSharpEvidence([], [], []));
    }

    public static SourceEvidence Load(string path, string source)
        => new(SourceStatus.Present, CSharpEvidenceExtractor.Extract(path, source, ["NET5_0_OR_GREATER"]));

    public static CSharpEvidence Combine(CSharpEvidence left, CSharpEvidence right)
        => new(
            [.. left.Functions, .. right.Functions],
            [.. left.Constants, .. right.Constants],
            [.. left.Types, .. right.Types]);
}
