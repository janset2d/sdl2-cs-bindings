using System.Text.Json;
using Build.Context.Models;
using Build.Modules.Contracts;
using Cake.Core.IO;

namespace Build.Modules.Preflight;

public sealed class VcpkgManifestReader(IFileSystem fileSystem) : IVcpkgManifestReader
{
    private readonly IFileSystem _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));

    public VcpkgManifest Parse(string jsonContent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jsonContent);

        return DeserializeManifest(
            () => JsonSerializer.Deserialize<VcpkgManifest>(jsonContent),
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
            () => JsonSerializer.Deserialize<VcpkgManifest>(buffer.ToArray()),
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