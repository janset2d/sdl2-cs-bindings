using Cake.Common.IO;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace Build.Infrastructure.DotNet;

public sealed class NuGetProtocolFeedClient(ICakeContext cakeContext, ICakeLog log) : INuGetFeedClient
{
    private readonly ICakeContext _cakeContext = cakeContext ?? throw new ArgumentNullException(nameof(cakeContext));
    private readonly ICakeLog _log = log ?? throw new ArgumentNullException(nameof(log));

    public async Task<NuGetVersion?> GetLatestVersionAsync(
        string feedUrl,
        string authToken,
        string packageId,
        bool includePrerelease,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(feedUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(authToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);
        cancellationToken.ThrowIfCancellationRequested();

        var repository = CreateRepository(feedUrl, authToken);
        var resource = await repository.GetResourceAsync<FindPackageByIdResource>(cancellationToken);

        using var cache = new SourceCacheContext { NoCache = true };
        var versions = await resource.GetAllVersionsAsync(packageId, cache, NullLogger.Instance, cancellationToken);

        var latest = versions
            .Where(v => includePrerelease || !v.IsPrerelease)
            .OrderByDescending(v => v, VersionComparer.Default)
            .FirstOrDefault();

        _log.Verbose(
            "NuGetProtocolFeedClient resolved latest '{0}' on '{1}' (includePrerelease={2}): {3}",
            packageId,
            feedUrl,
            includePrerelease,
            latest?.ToNormalizedString() ?? "<none>");

        return latest;
    }

    public async Task<FilePath> DownloadAsync(
        string feedUrl,
        string authToken,
        string packageId,
        NuGetVersion version,
        DirectoryPath targetDir,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(feedUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(authToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(targetDir);
        cancellationToken.ThrowIfCancellationRequested();

        var repository = CreateRepository(feedUrl, authToken);
        var resource = await repository.GetResourceAsync<FindPackageByIdResource>(cancellationToken);

        using var cache = new SourceCacheContext { NoCache = true };

        var fileName = string.Concat(packageId, ".", version.ToNormalizedString(), ".nupkg");
        var targetPath = targetDir.CombineWithFilePath(fileName);

        _cakeContext.EnsureDirectoryExists(targetDir);

        // FileMode.Create truncates a stale partial file from a prior failed download.
        var targetFile = _cakeContext.FileSystem.GetFile(targetPath);
        await using var fileStream = targetFile.Open(FileMode.Create, FileAccess.Write, FileShare.None);
        var copied = await resource.CopyNupkgToStreamAsync(packageId, version, fileStream, cache, NullLogger.Instance, cancellationToken);

        if (!copied)
        {
            throw new InvalidOperationException(
                $"NuGet feed at '{feedUrl}' could not stream '{packageId}' {version.ToNormalizedString()}. " +
                "Verify the package was published and the auth token has read:packages scope.");
        }

        _log.Verbose("NuGetProtocolFeedClient downloaded '{0}' {1} -> '{2}'.", packageId, version.ToNormalizedString(), targetPath.FullPath);

        return targetPath;
    }

    public async Task PushAsync(
        string feedUrl,
        string authToken,
        FilePath nupkgPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(feedUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(authToken);
        ArgumentNullException.ThrowIfNull(nupkgPath);
        cancellationToken.ThrowIfCancellationRequested();

        var repository = CreateRepository(feedUrl, authToken);
        var resource = await repository.GetResourceAsync<PackageUpdateResource>(cancellationToken);

        // skipDuplicate=false so a re-push at the same version fails loud — operators
        // must bump the prerelease counter rather than accidentally republishing.
        await resource.Push(
            packagePaths: [nupkgPath.FullPath],
            symbolSource: null,
            timeoutInSecond: 60,
            disableBuffering: false,
            getApiKey: _ => authToken,
            getSymbolApiKey: _ => authToken,
            noServiceEndpoint: false,
            skipDuplicate: false,
            symbolPackageUpdateResource: null,
            log: NullLogger.Instance);

        _log.Verbose("NuGetProtocolFeedClient pushed '{0}' -> '{1}'.", nupkgPath.FullPath, feedUrl);
    }

    private static SourceRepository CreateRepository(string feedUrl, string authToken)
    {
        var packageSource = new PackageSource(feedUrl)
        {
            // GitHub Packages NuGet ignores username when bearer tokens are supplied;
            // any non-empty value works.
            Credentials = new PackageSourceCredential(
                source: feedUrl,
                username: "anonymous",
                passwordText: authToken,
                isPasswordClearText: true,
                validAuthenticationTypesText: null),
        };

        return Repository.Factory.GetCoreV3(packageSource);
    }
}
