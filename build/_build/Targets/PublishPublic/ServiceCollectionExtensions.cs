using Microsoft.Extensions.DependencyInjection;

namespace Build.Targets.PublishPublic;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPublishPublic(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Stub — task discovered by Cake via [TaskName]. PD-7 implementation in Phase 2b.
        return services;
    }
}
