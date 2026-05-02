using Build.Shared.Strategy;
using Microsoft.Extensions.DependencyInjection;

namespace Build.Features.Preflight;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPreflightFeature(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Strategy resolver (Shared/Strategy seam) — Preflight is the primary consumer
        // via IStrategyCoherenceValidator. Other features that need strategy resolution
        // pick it up transitively from this registration.
        services.AddSingleton<IStrategyResolver, StrategyResolver>();

        services.AddSingleton<IVersionConsistencyValidator, VersionConsistencyValidator>();
        services.AddSingleton<IStrategyCoherenceValidator, StrategyCoherenceValidator>();
        services.AddSingleton<ICoreLibraryIdentityValidator, CoreLibraryIdentityValidator>();
        services.AddSingleton<IUpstreamVersionAlignmentValidator, UpstreamVersionAlignmentValidator>();
        services.AddSingleton<ICsprojPackContractValidator, CsprojPackContractValidator>();

        services.AddSingleton<PreflightReporter>();
        services.AddSingleton<PreflightPipeline>();

        return services;
    }
}
