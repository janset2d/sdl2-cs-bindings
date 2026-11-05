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

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
