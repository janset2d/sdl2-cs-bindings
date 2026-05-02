using Microsoft.Extensions.DependencyInjection;

namespace Build.Features.Diagnostics;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDiagnosticsFeature(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<InspectHarvestedDependenciesPipeline>();

        return services;
    }
}
