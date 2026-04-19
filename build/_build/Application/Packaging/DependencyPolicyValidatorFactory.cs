using Build.Context.Models;
using Build.Domain.Runtime;
using Build.Domain.Strategy;
using Build.Domain.Strategy.Models;

namespace Build.Application.Packaging;

public sealed class DependencyPolicyValidatorFactory(
    ManifestConfig manifest,
    IRuntimeProfile runtimeProfile,
    IPackagingStrategy packagingStrategy)
{
    private readonly ManifestConfig _manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
    private readonly IRuntimeProfile _runtimeProfile = runtimeProfile ?? throw new ArgumentNullException(nameof(runtimeProfile));
    private readonly IPackagingStrategy _packagingStrategy = packagingStrategy ?? throw new ArgumentNullException(nameof(packagingStrategy));

    public IDependencyPolicyValidator Create()
    {
        var validationMode = _manifest.PackagingConfig.ValidationMode;

        return _packagingStrategy.Model switch
        {
            PackagingModel.HybridStatic => new HybridStaticValidator(_runtimeProfile, _packagingStrategy, validationMode),
            PackagingModel.PureDynamic => new PureDynamicValidator(validationMode),
            _ => throw new InvalidOperationException($"Unsupported packaging model '{_packagingStrategy.Model}'."),
        };
    }
}
