using System.IO.Compression;
using System.Text.Json;
using Build.Host.Cake;
using Build.Results;
using Cake.Core;
using Cake.Core.IO;

namespace Build.Data.NativePackageMetadata;

public interface INativePackageMetadataRepository
{
    Task WriteAsync(FilePath path, NativePackageMetadataDocument metadata, CancellationToken ct = default);

    Task<Result<NativePackageMetadataDocument, NativePackageMetadataError>> ReadFromPackageAsync(
        FilePath nativePackagePath,
        CancellationToken ct = default);
}

public sealed class NativePackageMetadataRepository(ICakeContext cakeContext) : INativePackageMetadataRepository
{
    private const string MetadataEntryName = "janset-native-metadata.json";

    private readonly ICakeContext _cakeContext = cakeContext ?? throw new ArgumentNullException(nameof(cakeContext));

    public async Task WriteAsync(FilePath path, NativePackageMetadataDocument metadata, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(metadata);
        ct.ThrowIfCancellationRequested();

        await _cakeContext.WriteJsonAsync(path, metadata).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
    }

    public async Task<Result<NativePackageMetadataDocument, NativePackageMetadataError>> ReadFromPackageAsync(
        FilePath nativePackagePath,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(nativePackagePath);
        ct.ThrowIfCancellationRequested();

        var file = _cakeContext.FileSystem.GetFile(nativePackagePath);
        if (!file.Exists)
        {
            return Failure(
                $"Native package '{nativePackagePath.GetFilename().FullPath}' is missing, metadata file cannot be validated.");
        }

        try
        {
            await using var stream = file.OpenRead();
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
            var metadataEntry = archive.Entries.SingleOrDefault(entry =>
                string.Equals(entry.FullName, MetadataEntryName, StringComparison.OrdinalIgnoreCase));

            if (metadataEntry is null)
            {
                return Failure(
                    $"Native package '{nativePackagePath.GetFilename().FullPath}' does not contain root metadata file '{MetadataEntryName}'.");
            }

            ct.ThrowIfCancellationRequested();

            var metadataContent = await ReadEntryContentAsync(metadataEntry, ct).ConfigureAwait(false);
            var metadata = CakeJsonExtensions.DeserializeJson<NativePackageMetadataDocument>(metadataContent);

            if (metadata is null)
            {
                return Failure(
                    $"Metadata file in '{nativePackagePath.GetFilename().FullPath}' deserialized to null.");
            }

            return Result<NativePackageMetadataDocument, NativePackageMetadataError>.Success(metadata);
        }
        catch (JsonException ex)
        {
            return Failure(
                $"Metadata file in '{nativePackagePath.GetFilename().FullPath}' is not valid JSON: {ex.Message}",
                ex);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
        {
            return Failure(
                $"Native package '{nativePackagePath.GetFilename().FullPath}' could not be read while validating metadata: {ex.Message}",
                ex);
        }
    }

    private static async Task<string> ReadEntryContentAsync(ZipArchiveEntry entry, CancellationToken ct)
    {
#pragma warning disable CA1849, S6966 // ZipArchiveEntry.Open sync used intentionally for small package metadata reads.
        using var stream = entry.Open();
#pragma warning restore CA1849, S6966
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(ct).ConfigureAwait(false);
    }

    private static Result<NativePackageMetadataDocument, NativePackageMetadataError> Failure(string message, Exception? exception = null)
    {
        return Result<NativePackageMetadataDocument, NativePackageMetadataError>.Failure(new NativePackageMetadataError(message, exception));
    }
}
