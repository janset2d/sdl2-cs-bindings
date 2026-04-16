using Cake.Core.IO;

namespace Build.Tests.Fixtures;

public static class WorkspaceFiles
{
    private static readonly FileSystem FileSystem = new();

    public static DirectoryPath RepoRoot { get; } = ResolveRepoRoot();

    public static DirectoryPath BuildDir => RepoRoot.Combine("build");

    public static FilePath ManifestPath => BuildDir.CombineWithFilePath("manifest.json");

    public static FilePath VcpkgManifestPath => RepoRoot.CombineWithFilePath("vcpkg.json");

    public static bool Exists(FilePath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return FileSystem.GetFile(path).Exists;
    }

    public static string ReadAllText(FilePath path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var file = FileSystem.GetFile(path);
        using var stream = file.OpenRead();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static async Task<string> ReadAllTextAsync(FilePath path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var file = FileSystem.GetFile(path);
        using var stream = file.OpenRead();
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    private static DirectoryPath ResolveRepoRoot()
    {
        var repoRoot = new DirectoryPath(AppContext.BaseDirectory)
            .GetParent()?.GetParent()?.GetParent()?.GetParent()?.GetParent()?.Collapse();

        return repoRoot ?? throw new InvalidOperationException("Unable to resolve the workspace repo root from AppContext.BaseDirectory.");
    }
}
