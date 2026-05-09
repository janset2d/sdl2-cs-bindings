using Microsoft.Extensions.DependencyInjection;

namespace Build.Features.Packaging;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the still-pipeline-shaped Packaging services: post-pack validator collaborators
    /// (NativePackageMetadataValidator, ReadmeMappingTableValidator), generators
    /// (NativePackageMetadataGenerator, ReadmeMappingTableGenerator), and the surviving
    /// <see cref="IPackageConsumerSmokePipeline"/> orchestrator. Pack-stage orchestration
    /// migrated to <c>Targets/Package/</c> in S13 (P7); the <c>IPackageOutputValidator</c>
    /// implementation is registered alongside the other build-host validators in
    /// <c>Validation/ServiceCollectionExtensions.AddValidators()</c>.
    /// </summary>
    public static IServiceCollection AddPackagingFeature(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Generators consumed by Pack-stage orchestration (PackageTask + PackageFamilyPacker).
        // PackageOutputValidator + its two collaborator validators (NativePackageMetadataValidator,
        // ReadmeMappingTableValidator) live in AddValidators() to keep the cross-cutting validator
        // cohort self-sufficient.
        services.AddSingleton<INativePackageMetadataGenerator, NativePackageMetadataGenerator>();
        services.AddSingleton<IReadmeMappingTableGenerator, ReadmeMappingTableGenerator>();

        // ConsumerSmoke pipeline (P9 migration target).
        services.AddSingleton<IPackageConsumerSmokePipeline, PackageConsumerSmokePipeline>();

        return services;
    }
}
