using Build.Host.Paths;
using Build.Integrations.Vcpkg;
using Cake.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Build.Repositories;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IManifestRepository>(provider =>
        {
            var context = provider.GetRequiredService<ICakeContext>();
            var paths = provider.GetRequiredService<IPathService>();
            return new ManifestRepository(context, paths.GetManifestFile());
        });

        services.AddSingleton<IVcpkgManifestRepository>(provider =>
        {
            var context = provider.GetRequiredService<ICakeContext>();
            var reader = provider.GetRequiredService<IVcpkgManifestReader>();
            var paths = provider.GetRequiredService<IPathService>();
            return new VcpkgManifestRepository(context, reader, paths.GetVcpkgManifestFile());
        });

        services.AddSingleton<IVersionFileRepository>(provider =>
        {
            var context = provider.GetRequiredService<ICakeContext>();
            return new VersionFileRepository(context);
        });

        services.AddSingleton<IHarvestStatusRepository, HarvestStatusRepository>();

        return services;
    }
}
