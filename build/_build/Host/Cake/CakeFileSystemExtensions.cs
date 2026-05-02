using Cake.Common.IO;
using Cake.Core;
using Cake.Core.IO;

namespace Build.Host.Cake;

public static class CakeFileSystemExtensions
{
    /// <summary>
    /// Cake-native asynchronous UTF-8 text read. Thin wrapper over
    /// <see cref="IFileSystem.GetFile(FilePath)"/> + <see cref="StreamReader.ReadToEndAsync()"/>
    /// that keeps reads off raw <c>System.IO</c> (preserves testability against
    /// <see cref="Cake.Testing.FakeFileSystem"/>) and surfaces missing-file as a
    /// <see cref="CakeException"/>. Used for non-JSON text artifacts (harvest status files,
    /// Cobertura XML fragments, MSBuild props generation).
    /// </summary>
    public static async Task<string> ReadAllTextAsync(this ICakeContext cakeContext, FilePath filePath)
    {
        ArgumentNullException.ThrowIfNull(cakeContext);
        ArgumentNullException.ThrowIfNull(filePath);

        if (!cakeContext.FileExists(filePath))
        {
            throw new CakeException($"File not found at: {filePath.FullPath}");
        }

        var file = cakeContext.FileSystem.GetFile(filePath);
        using var stream = file.OpenRead();
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    /// <summary>
    /// Cake-native asynchronous UTF-8 text write. Creates the target directory via
    /// <see cref="DirectoryExtensions.EnsureDirectoryExists"/>, opens a fresh stream with
    /// <c>FileMode.Create</c> (overwrites existing content), and writes <paramref name="content"/>
    /// verbatim. For JSON serialization prefer <see cref="CakeJsonExtensions.WriteJsonAsync{TModel}"/>;
    /// this helper stays for arbitrary strings (MSBuild props, pre-formatted reports, smoke license
    /// manifests, etc.).
    /// </summary>
    public static async Task WriteAllTextAsync(this ICakeContext cakeContext, FilePath filePath, string content)
    {
        ArgumentNullException.ThrowIfNull(cakeContext);
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(content);

        cakeContext.EnsureDirectoryExists(filePath.GetDirectory());

        var file = cakeContext.FileSystem.GetFile(filePath);
        await using var stream = file.Open(FileMode.Create, FileAccess.Write, FileShare.None);
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(content);
    }
}
