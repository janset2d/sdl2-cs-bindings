using Build.Domain.Packaging.Models;
using Build.Domain.Paths;

namespace Build.Application.Packaging;

public sealed class ArtifactSourceResolverFactory(
    LocalArtifactSourceResolver localArtifactSourceResolver,
    RemoteArtifactSourceResolver remoteArtifactSourceResolver,
    IPathService pathService)
{
    private readonly LocalArtifactSourceResolver _localArtifactSourceResolver = localArtifactSourceResolver ?? throw new ArgumentNullException(nameof(localArtifactSourceResolver));
    private readonly RemoteArtifactSourceResolver _remoteArtifactSourceResolver = remoteArtifactSourceResolver ?? throw new ArgumentNullException(nameof(remoteArtifactSourceResolver));
    private readonly IPathService _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));

    public IArtifactSourceResolver Create(string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        return source.Trim().ToLowerInvariant() switch
        {
            "local" => _localArtifactSourceResolver,
            "remote" or "remote-internal" => _remoteArtifactSourceResolver,
            "release" or "release-public" => new UnsupportedArtifactSourceResolver(_pathService, ArtifactProfile.ReleasePublic, "release"),
            _ => throw new InvalidOperationException(
                $"Unsupported --source value '{source}'. Allowed values: local, remote, release."),
        };
    }
}
