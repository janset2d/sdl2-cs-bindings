using Build.Context;
using Build.Domain.Packaging.Models;
using Build.Domain.Paths;
using Build.Infrastructure.Paths;
using Cake.Core;
using Cake.Core.IO;

namespace Build.Application.Packaging;

public abstract class StubArtifactSourceResolverBase(IPathService pathService) : IArtifactSourceResolver
{
    private readonly IPathService _pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));

    public abstract ArtifactProfile Profile { get; }

    public DirectoryPath LocalFeedPath => _pathService.PackagesOutput;

    public Task PrepareFeedAsync(BuildContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        throw BuildNotImplemented("prepare local feed");
    }

    public Task WriteConsumerOverrideAsync(BuildContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        throw BuildNotImplemented("write smoke local override");
    }

    protected abstract string SourceArgumentLabel { get; }

    private CakeException BuildNotImplemented(string operation)
    {
        return new CakeException(
            $"SetupLocalDev --source={SourceArgumentLabel} is accepted but not implemented in Phase 2a. Cannot {operation}; {Profile} artifact acquisition lands in Phase 2b.");
    }
}
