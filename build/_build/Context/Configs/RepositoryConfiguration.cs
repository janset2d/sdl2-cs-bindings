using Cake.Core.IO;

namespace Build.Context.Configs;

/// <summary>
/// Holds the determined repository root path.
/// </summary>
/// <param name="RepoRoot">The absolute path to the repository root.</param>
public record RepositoryConfiguration(DirectoryPath RepoRoot);
