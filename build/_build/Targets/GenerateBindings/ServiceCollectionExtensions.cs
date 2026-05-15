using Build.Targets.GenerateBindings.HeaderSet;
using Build.Targets.GenerateBindings.Parsing;
using Microsoft.Extensions.DependencyInjection;

namespace Build.Targets.GenerateBindings;

internal static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGenerateBindings(this IServiceCollection services)
    {
        services.AddSingleton<ParseDiagnosticFormatter>();
        services.AddSingleton<CppAstParseRunner>();
        services.AddSingleton<HeaderSetResolver>();
        services.AddSingleton<HeaderSetFingerprintCalculator>();

        return services;
    }
}
