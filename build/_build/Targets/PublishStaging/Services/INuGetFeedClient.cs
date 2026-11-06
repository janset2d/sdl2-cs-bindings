using Cake.Core.IO;
using NuGet.Versioning;

namespace Build.Targets.PublishStaging.Services;

public interface INuGetFeedClient
{
    public Task<NuGetVersion?> GetLatestVersionAsync(string feedUrl, string authToken, string packageId, bool includePrerelease, CancellationToken ct = default);

    public Task<FilePath> DownloadAsync(string feedUrl, string authToken, string packageId, NuGetVersion version, DirectoryPath targetDir, CancellationToken ct = default);

    public Task PushAsync(string feedUrl, string authToken, FilePath nupkgPath, CancellationToken ct = default);
}
