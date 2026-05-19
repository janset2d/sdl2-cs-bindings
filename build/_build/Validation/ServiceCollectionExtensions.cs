using Build.Validation.BindingGeneration;
using Build.Validation.Harvesting;
using Build.Validation.Manifest;
using Build.Validation.NativeSmoke;
using Build.Validation.Packaging;
using Build.Validation.Vcpkg;
using Build.Validation.Versioning;
using Microsoft.Extensions.DependencyInjection;

namespace Build.Validation;

/// <summary>
/// Single registration point for every build-host validator. Validators live under
/// <c>Validation/Manifest/</c>, <c>Validation/Versioning/</c>, <c>Validation/Packaging/</c>,
/// <c>Validation/Harvesting/</c>, and <c>Validation/NativeSmoke/</c> — the alt-folders
/// organize them by domain while the canonical lookup point stays here. Composition root
/// calls this once; PreFlightCheck, ResolveVersionsFromExplicit, Package, Harvest, and
/// NativeSmoke all consume validators from DI. Root <c>Validation/</c> is the
/// explicit interface-friendly exception for cross-cutting build rules.
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
        services.AddSingleton<IHybridStaticLeakValidator, HybridStaticLeakValidator>();
        services.AddSingleton<IHarvestPreconditionsValidator, HarvestPreconditionsValidator>();
        services.AddSingleton<INativeSmokePreconditionsValidator, NativeSmokePreconditionsValidator>();
        services.AddSingleton<IPackageConsumerSmokePreconditionsValidator, PackageConsumerSmokePreconditionsValidator>();
        services.AddSingleton<IOverlayPortVersionCoherenceValidator, OverlayPortVersionCoherenceValidator>();

        // Binding-generation validators. IBindingFamilyValidator implementations
        // share a single registration interface so IEnumerable<IBindingFamilyValidator>
        // resolves to all of them; GenerateBindingsTask filters the IEnumerable by
        // manifest.binding_generation.validators[id] = true per family per run.
        // Adding a new validator: implement IBindingFamilyValidator, register here,
        // flip the manifest key.
        services.AddSingleton<IBindingFamilyValidator, DynapiCoherenceValidator>();
        services.AddSingleton<IBindingFamilyValidator, NeutralViewNonEmptyValidator>();
        services.AddSingleton<IBindingFamilyValidator, RequiredFunctionsEmittedValidator>();
        services.AddSingleton<IBindingFamilyValidator, SemanticTypeConsistencyValidator>();

        return services;
    }
}
