using Build.Data.Manifest;
using Build.Runtime;
using Build.Validation.Harvesting;
using Build.Validation.Manifest;
using Build.Validation.NativeSmoke;
using Build.Validation.Packaging;
using Build.Validation.Versioning;
using Microsoft.Extensions.DependencyInjection;

namespace Build.Validation;

/// <summary>
/// Single registration point for every build-host validator. Validators live under
/// <c>Validation/Manifest/</c>, <c>Validation/Versioning/</c>, <c>Validation/Packaging/</c>,
/// <c>Validation/Harvesting/</c>, and <c>Validation/NativeSmoke/</c> — the alt-folders
/// organize them by domain while the canonical lookup point stays here. Composition root
/// calls this once; PreFlightCheck, ResolveVersionsFromExplicit, Package, Harvest, and
/// NativeSmoke all consume validators from DI. ADR-002 §8 amendment (S12 + S13 + S14)
/// declares root <c>Validation/</c> an explicit IFoo-interface exception.
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
        services.AddSingleton<ISatelliteUpperBoundValidator, SatelliteUpperBoundValidator>();
        services.AddSingleton<IPackageOutputValidator, PackageOutputValidator>();
        services.AddSingleton<IHybridStaticLeakValidator>(sp =>
        {
            var manifest = sp.GetRequiredService<ManifestConfig>();
            var profile = sp.GetRequiredService<IRuntimeProfile>();
            return new HybridStaticLeakValidator(
                profile,
                manifest.CoreLibrary.VcpkgName,
                manifest.PackagingConfig.ValidationMode);
        });
        services.AddSingleton<IHarvestPreconditionsValidator, HarvestPreconditionsValidator>();
        services.AddSingleton<INativeSmokePreconditionsValidator, NativeSmokePreconditionsValidator>();
        services.AddSingleton<IPackageConsumerSmokePreconditionsValidator, PackageConsumerSmokePreconditionsValidator>();

        return services;
    }
}
