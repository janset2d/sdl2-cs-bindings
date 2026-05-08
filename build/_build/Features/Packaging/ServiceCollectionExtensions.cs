using Build.Shared.Packaging;
using Microsoft.Extensions.DependencyInjection;

namespace Build.Features.Packaging;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Packaging feature's services: post-pack validators, native package
    /// metadata + README mapping table generators, and the <see cref="IPackagePipeline"/> +
    /// <see cref="IPackageConsumerSmokePipeline"/> orchestrators.
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

        return services;
    }
}
