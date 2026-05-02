namespace Build.Shared.Packaging;

public sealed class DotNetPackError : PackagingError
{
    public DotNetPackError(string message, string? projectPath = null, Exception? exception = null) : base(message, exception)
    {
        ProjectPath = projectPath;
    }

    /// <summary>
    /// The csproj that was being packed when the failure occurred (canonical full-path
    /// string per ADR-004 §2.6 Shared no-Cake invariant), when available.
    /// </summary>
    public string? ProjectPath { get; }
}
