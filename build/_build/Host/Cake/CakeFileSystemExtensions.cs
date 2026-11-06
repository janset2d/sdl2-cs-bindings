using Cake.Common.IO;
using Cake.Core;
using Cake.Core.IO;

namespace Build.Host.Cake;

public static class CakeFileSystemExtensions
{
    /// <summary>Reads UTF-8 text through Cake and reports missing files as <see cref="CakeException"/>.</summary>
    public static async Task<string> ReadAllTextAsync(this ICakeContext cakeContext, FilePath filePath)
    {
        ArgumentNullException.ThrowIfNull(cakeContext);
        ArgumentNullException.ThrowIfNull(filePath);

        if (!cakeContext.FileExists(filePath))
        {
            throw new CakeException($"File not found at: {filePath.FullPath}");
        }

        var file = cakeContext.FileSystem.GetFile(filePath);
        await using var stream = file.OpenRead();
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    /// <summary>Writes UTF-8 text through Cake, creating the parent directory and replacing existing content.</summary>
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
