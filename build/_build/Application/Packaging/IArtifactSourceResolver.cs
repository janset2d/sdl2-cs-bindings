using Build.Context;
using Build.Domain.Packaging.Models;
using Cake.Core.IO;

namespace Build.Application.Packaging;

public interface IArtifactSourceResolver
{
    ArtifactProfile Profile { get; }

    DirectoryPath LocalFeedPath { get; }

    Task PrepareFeedAsync(BuildContext context, CancellationToken cancellationToken = default);

    Task WriteConsumerOverrideAsync(BuildContext context, CancellationToken cancellationToken = default);
}
