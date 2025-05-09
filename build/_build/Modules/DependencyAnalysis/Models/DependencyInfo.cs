namespace Build.Modules.DependencyAnalysis.Models;

public record DependencyInfo
{
    public required string Path { get; init; }

    public required string Package { get; init; }

    public ISet<string> Sources { get; init; } = new HashSet<string>(StringComparer.Ordinal);
}
