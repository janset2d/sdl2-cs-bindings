using Build.Targets.GenerateBindings.HeaderSet;
using Build.Targets.GenerateBindings.Parsing;
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

        // Validators (IBindingPublicApiCoherenceValidator + IBindingFamilyValidator
        // implementations) live under Build.Validation.BindingGeneration and register
        // via AddValidators() per the canonical convention — see
        // Build.Validation.ServiceCollectionExtensions.
        return services;
    }
}
