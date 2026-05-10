using System.Text.Json;
using Build.Host.Cake;
using Build.Manifest;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.IO;

namespace Build.Data.Manifest;

/// <summary>
/// File-backed repository for the repository-root <c>vcpkg.json</c> manifest. Loads the
/// canonical file via <see cref="Cake.Core.ICakeContext.FileSystem"/>, deserializes through
/// the project's central JSON surface, and surfaces missing-file or invalid-JSON failures as
/// <see cref="Cake.Core.CakeException"/> with operator-friendly messages.
/// </summary>
public interface IVcpkgManifestRepository
{
    VcpkgManifest Load();
}

public sealed class VcpkgManifestRepository(ICakeContext context, FilePath vcpkgManifestPath)
    : IVcpkgManifestRepository
{
    private readonly ICakeContext _context = context ?? throw new ArgumentNullException(nameof(context));
    private readonly FilePath _vcpkgManifestPath = vcpkgManifestPath ?? throw new ArgumentNullException(nameof(vcpkgManifestPath));

    /// <inheritdoc />
    public VcpkgManifest Load()
    {
        if (!_context.FileExists(_vcpkgManifestPath))
        {
            throw new CakeException(
                $"VcpkgManifestRepository cannot load vcpkg manifest: file does not exist at '{_vcpkgManifestPath.FullPath}'. " +
                "Run from the repository root or pass --repo-root to point Cake at a valid checkout.");
        }

        try
        {
            var file = _context.FileSystem.GetFile(_vcpkgManifestPath);
            using var stream = file.OpenRead();
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);

            var manifest = CakeJsonExtensions.DeserializeJson<VcpkgManifest>(buffer.ToArray());
            return manifest
                ?? throw new CakeException(
                    $"VcpkgManifestRepository: vcpkg manifest at '{_vcpkgManifestPath.FullPath}' deserialized to null. " +
                    "The file is syntactically valid JSON but evaluates to a null object — re-emit the file or fix its contents.");
        }
        catch (JsonException ex)
        {
            throw new CakeException(
                $"VcpkgManifestRepository: vcpkg manifest at '{_vcpkgManifestPath.FullPath}' contains invalid JSON: {ex.Message}. " +
                "Re-run vcpkg install or fix the file syntax.",
                ex);
        }
    }
}
