using Build.Context.Models;
using Build.Domain.Preflight.Results;

namespace Build.Domain.Preflight;

public interface ICoreLibraryIdentityValidator
{
    CoreLibraryIdentityResult Validate(ManifestConfig manifestConfig);
}
