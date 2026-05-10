using Build.Results;

namespace Build.Targets.Package.Models;

public sealed class DotNetPackError(string message, string? projectPath = null, Exception? exception = null) : BuildError(message, exception)
{
    /// <summary>
    /// The csproj that was being packed when the failure occurred (canonical full-path
    /// string, Shared no-Cake invariant), when available.
    /// </summary>
    public string? ProjectPath { get; } = projectPath;
}
