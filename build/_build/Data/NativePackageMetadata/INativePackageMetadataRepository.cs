using Build.Results;
using Cake.Core.IO;

namespace Build.Data.NativePackageMetadata;

public interface INativePackageMetadataRepository
{
    public Task WriteAsync(FilePath path, NativePackageMetadataDocument metadata, CancellationToken ct = default);

    public Task<Result<NativePackageMetadataDocument, NativePackageMetadataError>> ReadFromPackageAsync(
        FilePath nativePackagePath,
        CancellationToken ct = default);
}
