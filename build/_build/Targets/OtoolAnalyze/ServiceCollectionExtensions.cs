using Build.Targets.OtoolAnalyze.Reporting;
using Microsoft.Extensions.DependencyInjection;

namespace Build.Targets.OtoolAnalyze;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOtoolAnalyzeTarget(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<OtoolReporter>();
        return services;
    }
}
