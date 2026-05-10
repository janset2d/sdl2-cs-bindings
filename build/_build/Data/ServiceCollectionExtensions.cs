using Build.Data.Harvest;
using Build.Data.Manifest;
using Build.Data.Versions;
using Build.Host.Paths;
using Cake.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Build.Data;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddData(this IServiceCollection services)
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
            var paths = provider.GetRequiredService<IPathService>();
            return new VcpkgManifestRepository(context, paths.GetVcpkgManifestFile());
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
