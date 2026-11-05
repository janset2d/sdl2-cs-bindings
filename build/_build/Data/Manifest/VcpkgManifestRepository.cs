using System.Text.Json;
using Build.Data.Manifest.Models;
using Build.Host.Cake;
using Cake.Common.IO;
using Cake.Core;
using Cake.Core.IO;

namespace Build.Data.Manifest;

/// <summary>
/// File-backed repository for <c>vcpkg.json</c> manifests. Path is supplied per call —
/// the repository is state-less so the same instance services every caller and every
/// manifest file across the build host. Missing-file / invalid-JSON / null-payload all
/// surface as <see cref="Cake.Core.CakeException"/>; iterating callers pre-check
/// existence with <see cref="Cake.Common.IO.FileAliases.FileExists"/> when "no file" and
/// "invalid file" need to be distinguished.
/// </summary>
public interface IVcpkgManifestRepository
{
    VcpkgManifest Load(FilePath vcpkgManifestPath);
}

public sealed class VcpkgManifestRepository(ICakeContext context) : IVcpkgManifestRepository
{
    private readonly ICakeContext _context = context ?? throw new ArgumentNullException(nameof(context));

    public VcpkgManifest Load(FilePath vcpkgManifestPath)
    {
        ArgumentNullException.ThrowIfNull(vcpkgManifestPath);

        if (!_context.FileExists(vcpkgManifestPath))
        {
            throw new CakeException(
                $"VcpkgManifestRepository cannot load vcpkg manifest: file does not exist at '{vcpkgManifestPath.FullPath}'. " +
                "Run from the repository root or pass --repo-root to point Cake at a valid checkout.");
        }

        try
        {
            var file = _context.FileSystem.GetFile(vcpkgManifestPath);
            using var stream = file.OpenRead();
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);

            var manifest = CakeJsonExtensions.DeserializeJson<VcpkgManifest>(buffer.ToArray());
            return manifest
                ?? throw new CakeException(
                    $"VcpkgManifestRepository: vcpkg manifest at '{vcpkgManifestPath.FullPath}' deserialized to null. " +
                    "The file is syntactically valid JSON but evaluates to a null object — re-emit the file or fix its contents.");
        }
        catch (JsonException ex)
        {
            throw new CakeException(
                $"VcpkgManifestRepository: vcpkg manifest at '{vcpkgManifestPath.FullPath}' contains invalid JSON: {ex.Message}. " +
                "Re-run vcpkg install or fix the file syntax.",
                ex);
        }
    }
}
