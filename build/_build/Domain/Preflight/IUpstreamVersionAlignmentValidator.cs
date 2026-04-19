using Build.Context.Configs;
using Build.Context.Models;
using Build.Domain.Preflight.Results;

namespace Build.Domain.Preflight;

public interface IUpstreamVersionAlignmentValidator
{
    UpstreamVersionAlignmentResult Validate(ManifestConfig manifestConfig, PackageBuildConfiguration packageBuildConfiguration);
}
