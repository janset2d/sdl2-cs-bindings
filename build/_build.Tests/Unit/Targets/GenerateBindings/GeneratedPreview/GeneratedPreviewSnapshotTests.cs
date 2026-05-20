using System.Security.Cryptography;
using System.Text.Json;
using Build.Tests.Fixtures;

namespace Build.Tests.Unit.Targets.GenerateBindings.GeneratedPreview;

[GeneratedPreviewSnapshot]
public sealed class GeneratedPreviewSnapshotTests
{
    [Test]
    public Task GeneratedPreview_Should_Match_File_Inventory_Snapshot()
    {
        var root = FindRepositoryRoot();
        var previewRoot = Path.Combine(root, "artifacts", "generated-bindings-preview", "sdl2-core");
        if (!Directory.Exists(previewRoot))
        {
            throw new DirectoryNotFoundException("Generated preview directory not found. Run `dotnet run --file tools.cs -- generate-bindings` first.");
        }

        var entries = ReadEmittedFiles(previewRoot)
            .Order(StringComparer.Ordinal)
            .Select(relativePath => SnapshotEntry(previewRoot, relativePath))
            .ToArray();

        return Verify(entries);
    }

    private static IReadOnlyList<string> ReadEmittedFiles(string previewRoot)
    {
        var parseViewsPath = Path.Combine(previewRoot, "parse-views.json");
        using var document = JsonDocument.Parse(File.ReadAllBytes(parseViewsPath));

        return [.. document.RootElement
            .GetProperty("EmittedFiles")
            .EnumerateArray()
            .Select(element => element.GetString() ?? string.Empty)];
    }

    private static GeneratedPreviewFile SnapshotEntry(string previewRoot, string relativePath)
    {
        var path = Path.Combine(previewRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var bytes = File.ReadAllBytes(path);
        return new GeneratedPreviewFile(relativePath.Replace('\\', '/'), bytes.Length, Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "tools.cs")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root was not found from test output directory.");
    }

    private sealed record GeneratedPreviewFile(string RelativePath, long Length, string Sha256);
}
