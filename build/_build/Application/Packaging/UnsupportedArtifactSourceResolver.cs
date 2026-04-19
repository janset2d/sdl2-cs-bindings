using Build.Context;
using Build.Domain.Packaging.Models;
using Build.Domain.Paths;
using Cake.Core;
using Cake.Core.IO;

namespace Build.Application.Packaging;

public sealed class UnsupportedArtifactSourceResolver(
    IPathService pathService,
    ArtifactProfile profile,
    string sourceArgumentLabel) : IArtifactSourceResolver
{
    private readonly IPathService _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));

    public ArtifactProfile Profile { get; } = profile;

    public DirectoryPath LocalFeedPath => _pathService.PackagesOutput;

    public Task PrepareFeedAsync(BuildContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        throw BuildNotImplemented("prepare local feed", sourceArgumentLabel);
    }

    public Task WriteConsumerOverrideAsync(BuildContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        throw BuildNotImplemented("write smoke local override", sourceArgumentLabel);
    }

    private CakeException BuildNotImplemented(string operation, string source)
    {
        return new CakeException(
            $"SetupLocalDev --source={source} is accepted but not implemented in Phase 2a. Cannot {operation}; {Profile} artifact acquisition lands in Phase 2b.");
    }
}
