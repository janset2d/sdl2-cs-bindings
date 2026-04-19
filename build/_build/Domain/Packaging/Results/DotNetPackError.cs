using Cake.Core.IO;

namespace Build.Domain.Packaging.Results;

public sealed class DotNetPackError : PackagingError
{
    public DotNetPackError(string message, FilePath? projectPath = null, Exception? exception = null) : base(message, exception)
    {
        ProjectPath = projectPath;
    }

    /// <summary>
    /// The csproj that was being packed when the failure occurred, when available.
    /// </summary>
    public FilePath? ProjectPath { get; }
}
