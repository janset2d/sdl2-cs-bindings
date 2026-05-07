using Build.Host.Paths;
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

        services.AddSingleton<IVersionFileRepository>(provider =>
        {
            var context = provider.GetRequiredService<ICakeContext>();
            return new VersionFileRepository(context);
        });

        return services;
    }
}
