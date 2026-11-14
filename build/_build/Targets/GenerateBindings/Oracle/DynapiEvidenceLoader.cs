using Build.Host.Cake;
using Cake.Core;
using Cake.Core.IO;

namespace Build.Targets.GenerateBindings.Oracle;

internal static class DynapiEvidenceLoader
{
    public static async Task<DynapiEvidence> LoadAsync(ICakeContext context, DirectoryPath repoRoot)
    {
        var exports = new HashSet<string>(StringComparer.Ordinal);

        foreach (var path in FindDynapiExportPaths(context, repoRoot))
        {
            foreach (var export in DynapiParser.Parse(await context.ReadAllTextAsync(path)))
            {
                exports.Add(export);
            }
        }

        return exports.Count == 0
            ? new DynapiEvidence(SourceStatus.Missing, exports)
            : new DynapiEvidence(SourceStatus.Present, exports);
    }

    private static IEnumerable<FilePath> FindDynapiExportPaths(ICakeContext context, DirectoryPath repoRoot)
    {
        var candidateRoots = new[]
        {
            repoRoot.Combine("external/vcpkg/buildtrees/sdl2/src"),
            repoRoot.Combine("vcpkg_installed/vcpkg/blds/sdl2/src")
        };

        foreach (var candidateRoot in candidateRoots)
        {
            var directory = context.FileSystem.GetDirectory(candidateRoot);

            if (!directory.Exists)
            {
                continue;
            }

            foreach (var versionDirectory in directory.GetDirectories("*", SearchScope.Current).OrderBy(item => item.Path.FullPath, StringComparer.Ordinal))
            {
                var exportPath = versionDirectory.Path.CombineWithFilePath("src/dynapi/SDL2.exports");

                if (context.FileSystem.GetFile(exportPath).Exists)
                {
                    yield return exportPath;
                }
            }
        }
    }
}
