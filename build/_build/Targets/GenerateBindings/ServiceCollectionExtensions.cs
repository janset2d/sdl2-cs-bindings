using Build.Targets.GenerateBindings.Emit;
using Build.Targets.GenerateBindings.HeaderSet;
using Build.Targets.GenerateBindings.ModelBuilding;
using Build.Targets.GenerateBindings.Parse;
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
        services.AddSingleton<BindingModelBuilder>();
        services.AddSingleton<BindingEmitter>();
        services.AddSingleton<BindingFamilyGeneration>();

        // Validators (IBindingPublicApiCoherenceValidator + IBindingFamilyValidator
        // implementations) live under Build.Validation.BindingGeneration and register
        // via AddValidators() per the canonical convention — see
        // Build.Validation.ServiceCollectionExtensions.
        return services;
    }
}
