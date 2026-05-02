using Microsoft.Extensions.DependencyInjection;

namespace Build.Features.Vcpkg;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddVcpkgFeature(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<EnsureVcpkgDependenciesPipeline>();

        return services;
    }
}
