using Build.Host.Configuration;
using Build.Shared.Packaging;
using Build.Shared.Strategy;
using Microsoft.Extensions.DependencyInjection;

namespace Build.Features.Packaging;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Packaging feature's services: post-pack validators, native package
    /// metadata + README mapping table generators, the <see cref="IPackagePipeline"/> +
    /// <see cref="IPackageConsumerSmokePipeline"/> orchestrators, and the strategy
    /// factories that resolve <see cref="IPackagingStrategy"/> / <see cref="IDependencyPolicyValidator"/>
    /// from the host's <see cref="VcpkgConfiguration"/>.
    /// </summary>
    public static IServiceCollection AddPackagingFeature(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Post-pack validators
        services.AddSingleton<NativePackageMetadataValidator>();
        services.AddSingleton<ReadmeMappingTableValidator>();
        services.AddSingleton<IPackageOutputValidator, PackageOutputValidator>();
        services.AddSingleton<IG58CrossFamilyDepResolvabilityValidator, G58CrossFamilyDepResolvabilityValidator>();

        // Generators
        services.AddSingleton<INativePackageMetadataGenerator, NativePackageMetadataGenerator>();
        services.AddSingleton<IReadmeMappingTableGenerator, ReadmeMappingTableGenerator>();

        // Pipelines
        services.AddSingleton<IPackagePipeline, PackagePipeline>();
        services.AddSingleton<IPackageConsumerSmokePipeline, PackageConsumerSmokePipeline>();

        // Packaging-strategy + dependency-policy seam factories: resolve concrete
        // strategy / validator from the active VcpkgConfiguration / triplet shape.
        services.AddSingleton<PackagingStrategyFactory>();
        services.AddSingleton<IPackagingStrategy>(provider =>
            provider.GetRequiredService<PackagingStrategyFactory>().Create());

        services.AddSingleton<DependencyPolicyValidatorFactory>();
        services.AddSingleton<IDependencyPolicyValidator>(provider =>
            provider.GetRequiredService<DependencyPolicyValidatorFactory>().Create());

        return services;
    }
}
