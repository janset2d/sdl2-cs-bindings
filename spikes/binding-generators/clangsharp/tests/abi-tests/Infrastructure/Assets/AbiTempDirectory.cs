using System.Text;

namespace Janset.SDL2.AbiTests.Infrastructure.Assets;

internal sealed class AbiTempDirectory : IDisposable
{
    public AbiTempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "janset-sdl2-abi-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string GetFilePath(string fileName) => System.IO.Path.Combine(Path, fileName);

    public Task WriteAsciiFileAsync(string fileName, string content) => WriteBytesAsync(fileName, Encoding.ASCII.GetBytes(content));

    public async Task WriteBytesAsync(string fileName, byte[] content)
    {
#if NETFRAMEWORK
        using FileStream stream = new(GetFilePath(fileName), FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
        await stream.WriteAsync(content, 0, content.Length);
        await stream.FlushAsync();
#else
        await File.WriteAllBytesAsync(GetFilePath(fileName), content);
#endif
    }

    public async Task<string> ReadAsciiAsync(string fileName)
    {
#if NETFRAMEWORK
        using FileStream stream = new(GetFilePath(fileName), FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        using StreamReader reader = new(stream, Encoding.ASCII);
        return await reader.ReadToEndAsync();
#else
        return await File.ReadAllTextAsync(GetFilePath(fileName), Encoding.ASCII);
#endif
    }

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
