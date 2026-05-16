using Build.Targets.GenerateBindings.HeaderSet;
using Build.Targets.GenerateBindings.Parsing;
using Build.Validation.BindingGeneration;
using Microsoft.Extensions.DependencyInjection;

namespace Build.Targets.GenerateBindings;

internal static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGenerateBindings(this IServiceCollection services)
    {
        services.AddSingleton<ParseDiagnosticFormatter>();
        services.AddSingleton<ICppAstParseRunner, CppAstParseRunner>();
        services.AddSingleton<ILibclangVersionAsserter, LibclangVersionAsserter>();
        services.AddSingleton<HeaderSetResolver>();
        services.AddSingleton<HeaderSetFingerprintCalculator>();
        services.AddSingleton<IBindingPublicApiCoherenceValidator, BindingPublicApiCoherenceValidator>();

        return services;
    }
}
