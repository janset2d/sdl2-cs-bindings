using Build.Features.Packaging.ArtifactSourceResolvers;
using Build.Host.Configuration;
using Build.Shared.Strategy;
using Microsoft.Extensions.DependencyInjection;

namespace Build.Features.Packaging;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Packaging feature's services: post-pack validators, native package
    /// metadata + README mapping table generators, the <see cref="IPackagePipeline"/> +
    /// <see cref="IPackageConsumerSmokePipeline"/> orchestrators, the <see cref="IArtifactSourceResolver"/>
    /// factory chain (Local + Remote + Unsupported per ADR-001 §2.7), and the strategy
    /// factories that resolve <see cref="IPackagingStrategy"/> / <see cref="IDependencyPolicyValidator"/>
    /// from the host's <see cref="VcpkgConfiguration"/>.
    /// </summary>
    /// <remarks>
    /// The active artifact-source resolver depends on the operator's <c>--source</c> CLI
    /// argument (Local / Remote). Because that argument is parsed in <c>Program.cs</c>
    /// before DI configuration, the resolver factory closure captures the source string
    /// from the call site rather than looking it up via DI. Composition root passes the
    /// source string to <see cref="AddPackagingFeature"/>.
    /// </remarks>
    public static IServiceCollection AddPackagingFeature(this IServiceCollection services, string source)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

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

        // Artifact-source resolvers (Local / Remote / Unsupported); operator's --source CLI
        // argument selects the active one via factory dispatch.
        services.AddSingleton<LocalArtifactSourceResolver>();
        services.AddSingleton<RemoteArtifactSourceResolver>();
        services.AddSingleton<ArtifactSourceResolverFactory>();
        services.AddSingleton<IArtifactSourceResolver>(provider =>
            provider.GetRequiredService<ArtifactSourceResolverFactory>().Create(source));

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
