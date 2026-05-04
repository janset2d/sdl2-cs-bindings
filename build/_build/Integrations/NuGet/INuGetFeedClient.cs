using Cake.Core.IO;
using NuGet.Versioning;

namespace Build.Integrations.NuGet;

public interface INuGetFeedClient
{
    Task<NuGetVersion?> GetLatestVersionAsync(
        string feedUrl,
        string authToken,
        string packageId,
        bool includePrerelease,
        CancellationToken ct = default);

    Task<FilePath> DownloadAsync(
        string feedUrl,
        string authToken,
        string packageId,
        NuGetVersion version,
        DirectoryPath targetDir,
        CancellationToken ct = default);

    Task PushAsync(
        string feedUrl,
        string authToken,
        FilePath nupkgPath,
        CancellationToken ct = default);
}
