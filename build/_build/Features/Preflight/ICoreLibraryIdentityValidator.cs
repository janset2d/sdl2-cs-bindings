using Build.Shared.Manifest;

namespace Build.Features.Preflight;

public interface ICoreLibraryIdentityValidator
{
    CoreLibraryIdentityResult Validate(ManifestConfig manifestConfig);
}
