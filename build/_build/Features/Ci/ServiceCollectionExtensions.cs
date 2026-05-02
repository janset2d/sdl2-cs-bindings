using Microsoft.Extensions.DependencyInjection;

namespace Build.Features.Ci;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCiFeature(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<GenerateMatrixPipeline>();

        return services;
    }
}
