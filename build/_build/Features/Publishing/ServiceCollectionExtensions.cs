using Microsoft.Extensions.DependencyInjection;

namespace Build.Features.Publishing;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPublishingFeature(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<PublishPipeline>();

        return services;
    }
}
