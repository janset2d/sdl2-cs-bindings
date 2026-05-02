using Build.Features.LocalDev;
using Build.Host;
using Cake.Core.IO;
using NuGet.Versioning;

namespace Build.Features.Packaging.ArtifactSourceResolvers;

/// <summary>
/// Profile-aware consumer-feed seam (ADR-001 §2.7): given a resolved per-family version
/// mapping, prepare the feed (verify local nupkgs exist or stage a remote pull) and emit
/// the MSBuild override that points smoke consumers at it. Feed <em>production</em> (pack,
/// harvest, etc.) lives upstream in an Application-layer runner such as
/// <see cref="SetupLocalDevFlow"/>; the resolver only resolves from whatever is on
/// disk for its profile.
/// </summary>
public interface IArtifactSourceResolver
{
    ArtifactProfile Profile { get; }

    DirectoryPath LocalFeedPath { get; }

    Task PrepareFeedAsync(
        BuildContext context,
        IReadOnlyDictionary<string, NuGetVersion> versions,
        CancellationToken cancellationToken = default);

    Task WriteConsumerOverrideAsync(
        BuildContext context,
        IReadOnlyDictionary<string, NuGetVersion> versions,
        CancellationToken cancellationToken = default);
}
