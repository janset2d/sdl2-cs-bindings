using System.CommandLine;

namespace Build.Context.Options;

public static class RepositoryOptions
{
    public static readonly Option<DirectoryInfo?> RepoRooOption = new(
        aliases: ["--repo-root"],
        description: "Absolute path to the repository root. If not specified, calculated via git.")
    {
        IsRequired = false,
    };
}
