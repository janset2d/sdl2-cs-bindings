namespace Build.Tests.Fixtures.RepositoryGates;

internal static class GateScopes
{
    public static bool IsLinkScanned(string path) => path.EndsWith(".md", StringComparison.Ordinal);

    public static bool RequiresDate(string path) => IsLinkScanned(path)
        && path.StartsWith("docs/", StringComparison.Ordinal)
        && !path.StartsWith("docs/superpowers/", StringComparison.Ordinal);

    public static bool IsCompanionPathScanned(string path) => RequiresDate(path)
        || path is "AGENTS.md" or "CLAUDE.md" or "README.md" or "GLOSSARY.md" or "build/AGENTS.md" or "build/CLAUDE.md";

    public static bool IsCitationScanned(string path)
    {
        if (IsLinkScanned(path) || path.Contains(".verified.", StringComparison.Ordinal)
            || path.StartsWith("build/_build.Tests/Integration/RepositoryGates/", StringComparison.Ordinal)
            || path.StartsWith("build/_build.Tests/Unit/RepositoryGates/", StringComparison.Ordinal)
            || path.StartsWith("build/_build.Tests/Fixtures/RepositoryGates/", StringComparison.Ordinal))
        {
            return false;
        }

        if (!RepositoryFiles.Tracked.Contains(path) || RepositoryFiles.Submodules.Contains(path))
        {
            return false;
        }

        try
        {
            using var stream = File.OpenRead(Path.Combine(RepositoryFiles.Root, path));
            Span<byte> prefix = stackalloc byte[8192];
            var count = stream.ReadAtLeast(prefix, prefix.Length, throwOnEndOfStream: false);
            return !prefix[..count].Contains((byte)0);
        }
        catch (IOException error) when (error is FileNotFoundException or DirectoryNotFoundException)
        {
            return false;
        }
    }
}
