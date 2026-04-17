using Cake.Core.IO;

namespace Build.Modules.Packaging.Results;

public sealed class ProjectMetadataError : PackagingError
{
    public ProjectMetadataError(string message, FilePath? projectPath = null, Exception? exception = null)
        : base(message, exception)
    {
        ProjectPath = projectPath;
    }

    /// <summary>
    /// The csproj that was being queried when the failure occurred, when available.
    /// </summary>
    public FilePath? ProjectPath { get; }
}
