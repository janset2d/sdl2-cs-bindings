internal static class DynapiEvidenceLoader
{
    public static DynapiEvidence Load(string repoRoot)
    {
        var exports = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in FindDynapiExportPaths(repoRoot))
        {
            foreach (var export in DynapiParser.Parse(File.ReadAllText(path)))
            {
                exports.Add(export);
            }
        }

        return exports.Count == 0
            ? new DynapiEvidence(SourceStatus.Missing, exports)
            : new DynapiEvidence(SourceStatus.Present, exports);
    }

    private static IEnumerable<string> FindDynapiExportPaths(string repoRoot)
    {
        var candidateRoots = new[]
        {
            Path.Combine(repoRoot, "external", "vcpkg", "buildtrees", "sdl2", "src"),
            Path.Combine(repoRoot, "vcpkg_installed", "vcpkg", "blds", "sdl2", "src")
        };

        foreach (var candidateRoot in candidateRoots)
        {
            if (!Directory.Exists(candidateRoot))
            {
                continue;
            }

            foreach (var versionDirectory in Directory.EnumerateDirectories(candidateRoot).Order(StringComparer.Ordinal))
            {
                var exportPath = Path.Combine(versionDirectory, "src", "dynapi", "SDL2.exports");
                if (File.Exists(exportPath))
                {
                    yield return exportPath;
                }
            }
        }
    }
}
