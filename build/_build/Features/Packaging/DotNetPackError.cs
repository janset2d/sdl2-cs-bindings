using Cake.Core.IO;

namespace Build.Features.Packaging;

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
