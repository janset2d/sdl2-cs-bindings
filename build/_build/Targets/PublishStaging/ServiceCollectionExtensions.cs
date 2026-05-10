using Build.Targets.PublishStaging.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Build.Targets.PublishStaging;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPublishStaging(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<INuGetFeedClient, NuGetProtocolFeedClient>();

        // PublishStagingTask discovered by Cake via [TaskName].

        return services;
    }
}
