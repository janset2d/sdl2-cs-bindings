using Build.Targets.NativeSmoke.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Build.Targets.NativeSmoke;

/// <summary>
/// Registers the NativeSmoke target's collaborators. <c>IMsvcDevEnvironment</c> lives here
/// because NativeSmokeTask is its only consumer (Integrations/Msvc/ retired).
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNativeSmoke(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IMsvcDevEnvironment, MsvcDevEnvironment>();

        return services;
    }
}
