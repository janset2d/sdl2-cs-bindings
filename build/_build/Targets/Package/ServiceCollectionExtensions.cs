using Build.Data.ProjectMetadata;
using Build.Targets.Package.Reporting;
using Build.Targets.Package.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Build.Targets.Package;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers Pack-stage collaborators: dependency range normalizer, per-family packer,
    /// reporter, the two metadata generators (native + README mapping), and
    /// the cross-target project metadata reader in <c>Build.Data.ProjectMetadata</c> shared with
    /// PackageConsumerSmoke. Cross-cutting validators (HarvestReadiness, PackageOutput) come
    /// from <c>AddValidators()</c>. <see cref="PackageTask"/> itself is discovered by Cake
    /// Frosting from <c>[TaskName]</c> metadata; do not register it here.
    /// </summary>
    public static IServiceCollection AddPackage(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<DependencyRangeNormalizer>();
        services.AddSingleton<PackageReporter>();
        services.AddSingleton<PackageFamilyPacker>();
        services.AddSingleton<INativePackageMetadataGenerator, NativePackageMetadataGenerator>();
        services.AddSingleton<IReadmeMappingTableGenerator, ReadmeMappingTableGenerator>();
        services.AddSingleton<IProjectMetadataReader, ProjectMetadataReader>();

        return services;
    }
}
