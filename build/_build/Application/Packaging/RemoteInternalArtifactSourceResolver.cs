using Build.Domain.Packaging.Models;
using Build.Domain.Paths;
using Build.Infrastructure.Paths;

namespace Build.Application.Packaging;

public sealed class RemoteInternalArtifactSourceResolver(IPathService pathService) : StubArtifactSourceResolverBase(pathService)
{
    public override ArtifactProfile Profile => ArtifactProfile.RemoteInternal;

    protected override string SourceArgumentLabel => "remote";
}
