using Microsoft.Extensions.DependencyInjection;

namespace Build.Targets.PublishStaging;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPublishStaging(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // PublishStagingTask discovered by Cake via [TaskName].
        // INuGetFeedClient comes from AddIntegrations() (P10 relocation candidate).
        return services;
    }
}
