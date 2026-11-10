using System.CommandLine;

namespace Build.Host.Cli.Options;

public static class RepositoryOptions
{
    public static readonly Option<DirectoryInfo?> RepoRootOption = new("--repo-root")
    {
        Description = "Absolute path to the repository root. If not specified, calculated via git.",
    };
}
