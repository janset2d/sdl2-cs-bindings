using System.Text.Json;
using Build.Host.Cake;
using Build.Shared.Manifest;
using Cake.Core.IO;

namespace Build.Integrations.Vcpkg;

public sealed class VcpkgManifestReader(IFileSystem fileSystem) : IVcpkgManifestReader
{
    private readonly IFileSystem _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));

    public VcpkgManifest Parse(string jsonContent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jsonContent);

        return DeserializeManifest(
            () => CakeExtensions.DeserializeJson<VcpkgManifest>(jsonContent),
            nameof(jsonContent));
    }

    public VcpkgManifest ParseFile(FilePath path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var file = _fileSystem.GetFile(path);
        using var stream = file.OpenRead();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);

        return DeserializeManifest(
            () => CakeExtensions.DeserializeJson<VcpkgManifest>(buffer.ToArray()),
            nameof(path));
    }

    private static VcpkgManifest DeserializeManifest(Func<VcpkgManifest?> deserialize, string parameterName)
    {
        try
        {
            var manifest = deserialize();
            return manifest
                ?? throw new ArgumentException("vcpkg manifest JSON deserialized to null.", parameterName);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"Invalid vcpkg manifest JSON: {ex.Message}", parameterName, ex);
        }
    }
}
