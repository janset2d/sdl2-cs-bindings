using Build.Context.Models;
using Build.Modules.Preflight.Results;

namespace Build.Modules.Contracts;

public interface ICoreLibraryIdentityValidator
{
    CoreLibraryIdentityResult Validate(ManifestConfig manifestConfig);
}
