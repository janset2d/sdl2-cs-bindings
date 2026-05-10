namespace Build.Packaging;

public sealed class ProjectMetadataError : PackagingError
{
    public ProjectMetadataError(string message, string? projectPath = null, Exception? exception = null)
        : base(message, exception)
    {
        ProjectPath = projectPath;
    }

    /// <summary>
    /// The csproj that was being queried when the failure occurred (canonical full-path
    /// string, Shared no-Cake invariant), when available.
    /// </summary>
    public string? ProjectPath { get; }
}
