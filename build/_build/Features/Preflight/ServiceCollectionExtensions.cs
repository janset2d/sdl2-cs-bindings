using Build.Shared.Strategy;
using Build.Shared.Versioning;
using Microsoft.Extensions.DependencyInjection;

namespace Build.Features.Preflight;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPreflightFeature(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Strategy resolver (Shared/Strategy seam) — Preflight is the primary consumer
        // via StrategyCoherenceValidator. Other features that need strategy resolution
        // pick it up transitively from this registration.
        services.AddSingleton<IStrategyResolver, StrategyResolver>();

        services.AddSingleton<StrategyCoherenceValidator>();
        services.AddSingleton<IUpstreamVersionAlignmentValidator, UpstreamVersionAlignmentValidator>();
        services.AddSingleton<ICsprojPackContractValidator, CsprojPackContractValidator>();

        services.AddSingleton<PreflightReporter>();
        services.AddSingleton<PreflightPipeline>();

        return services;
    }
}
