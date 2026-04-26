using Cake.Core.IO;
using NuGet.Versioning;

namespace Build.Infrastructure.DotNet;

public interface INuGetFeedClient
{
    Task<NuGetVersion?> GetLatestVersionAsync(
        string feedUrl,
        string authToken,
        string packageId,
        bool includePrerelease,
        CancellationToken cancellationToken = default);

    Task<FilePath> DownloadAsync(
        string feedUrl,
        string authToken,
        string packageId,
        NuGetVersion version,
        DirectoryPath targetDir,
        CancellationToken cancellationToken = default);
}
