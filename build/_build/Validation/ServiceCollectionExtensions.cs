using Build.Validation.Manifest;
using Build.Validation.Packaging;
using Build.Validation.Versioning;
using Microsoft.Extensions.DependencyInjection;

namespace Build.Validation;

/// <summary>
/// Single registration point for every build-host validator. Validators live under
/// <c>Validation/Manifest/</c>, <c>Validation/Versioning/</c>, and <c>Validation/Packaging/</c>
/// — the alt-folders organize them by domain while the canonical lookup point stays here.
/// Composition root calls this once; PreFlightCheck, ResolveVersionsFromExplicit, and Package
/// all consume validators from DI.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddValidators(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IManifestFamilyNameInvariantValidator, ManifestFamilyNameInvariantValidator>();
        services.AddSingleton<IVersionConsistencyValidator, VersionConsistencyValidator>();
        services.AddSingleton<ICoreLibraryIdentityValidator, CoreLibraryIdentityValidator>();
        services.AddSingleton<ICsprojPackContractValidator, CsprojPackContractValidator>();
        services.AddSingleton<ICrossFamilyDependencyResolvabilityValidator, CrossFamilyDependencyResolvabilityValidator>();
        services.AddSingleton<IUpstreamVersionAlignmentValidator, UpstreamVersionAlignmentValidator>();
        services.AddSingleton<IHybridStaticOverlayValidator, HybridStaticOverlayValidator>();
        services.AddSingleton<IHarvestReadinessValidator, HarvestReadinessValidator>();
        services.AddSingleton<INativePackageMetadataValidator, NativePackageMetadataValidator>();
        services.AddSingleton<IReadmeMappingTableValidator, ReadmeMappingTableValidator>();
        services.AddSingleton<IPackageOutputValidator, PackageOutputValidator>();

        return services;
    }
}
