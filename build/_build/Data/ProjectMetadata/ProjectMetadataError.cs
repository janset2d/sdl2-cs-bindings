using Build.Results;

namespace Build.Data.ProjectMetadata;

public sealed class ProjectMetadataError(string message, string? projectPath = null, Exception? exception = null)
    : BuildError(message, exception)
{
    /// <summary>
    /// The csproj queried when the failure occurred, when available.
    /// </summary>
    public string? ProjectPath { get; } = projectPath;
}
