using Microsoft.Extensions.DependencyInjection;

namespace Build.Features.Info;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfoFeature(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<InfoPipeline>();

        return services;
    }
}
