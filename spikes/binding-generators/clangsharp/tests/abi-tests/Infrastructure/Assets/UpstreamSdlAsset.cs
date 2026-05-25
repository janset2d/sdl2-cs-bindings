namespace Janset.SDL2.AbiTests.Infrastructure.Assets;

internal static class UpstreamSdlAsset
{
    public static string Path(string relativePath)
    {
        string path = System.IO.Path.Combine(AppContext.BaseDirectory, "TestAssets", "sdl2-upstream", relativePath);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"SDL upstream test asset was not copied to output: {path}", path);
        }

        return path;
    }
}
