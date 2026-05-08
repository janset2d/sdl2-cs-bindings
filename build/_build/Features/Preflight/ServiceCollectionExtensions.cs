using Build.Shared.Versioning;
using Microsoft.Extensions.DependencyInjection;

namespace Build.Features.Preflight;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPreflightFeature(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<HybridStaticOverlayValidator>();
        services.AddSingleton<IUpstreamVersionAlignmentValidator, UpstreamVersionAlignmentValidator>();
        services.AddSingleton<ICsprojPackContractValidator, CsprojPackContractValidator>();

        services.AddSingleton<PreflightReporter>();
        services.AddSingleton<PreflightPipeline>();

        return services;
    }
}
