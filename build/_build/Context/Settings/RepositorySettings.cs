using Cake.Core.IO;

namespace Build.Context.Settings;

/// <summary>
/// Holds the determined repository root path.
/// </summary>
/// <param name="RepoRoot">The absolute path to the repository root.</param>
public record RepositorySettings(DirectoryPath RepoRoot);
