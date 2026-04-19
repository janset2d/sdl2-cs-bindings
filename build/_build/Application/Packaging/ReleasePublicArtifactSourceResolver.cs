using Build.Domain.Packaging.Models;
using Build.Domain.Paths;
using Build.Infrastructure.Paths;

namespace Build.Application.Packaging;

public sealed class ReleasePublicArtifactSourceResolver(IPathService pathService) : StubArtifactSourceResolverBase(pathService)
{
    public override ArtifactProfile Profile => ArtifactProfile.ReleasePublic;

    protected override string SourceArgumentLabel => "release";
}
