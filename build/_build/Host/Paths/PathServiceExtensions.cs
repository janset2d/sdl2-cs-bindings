using Cake.Core.IO;

namespace Build.Host.Paths;

public static class PathServiceExtensions
{
    /// <summary>The repository-relative form of a path under the repository root, as stamps, reports and log lines record it.</summary>
    public static string GetRepoRelativePath(this IPathService paths, FilePath path)
    {
        ArgumentNullException.ThrowIfNull(paths);
        return paths.RepoRoot.GetRelativePath(path).FullPath;
    }

    /// <inheritdoc cref="GetRepoRelativePath(IPathService, FilePath)"/>
    public static string GetRepoRelativePath(this IPathService paths, DirectoryPath path)
    {
        ArgumentNullException.ThrowIfNull(paths);
        return paths.RepoRoot.GetRelativePath(path).FullPath;
    }
}
