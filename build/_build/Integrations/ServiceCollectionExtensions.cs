using Build.Integrations.DotNet;
using Build.Integrations.Msvc;
using Build.Integrations.NuGet;
using Build.Integrations.Vcpkg;
using Build.Shared.Runtime;
using Microsoft.Extensions.DependencyInjection;
using DotNetRuntimeEnvironment = Build.Integrations.DotNet.DotNetRuntimeEnvironment;

namespace Build.Integrations;

/// <summary>
/// Composition-root grouping for non-Cake-Tool external adapters.
/// Each registration binds an interface from the <c>Build.Integrations.*</c>
/// namespace to its concrete implementation. This collapses the inline
/// integrations block in <c>Program.cs ConfigureBuildServices</c> into a single
/// <c>AddIntegrations()</c> call so the composition root reads as a feature
/// roster + cross-cutting groups.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIntegrations(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IPackageInfoProvider, VcpkgCliProvider>();
        services.AddSingleton<IVcpkgManifestReader, VcpkgManifestReader>();
        services.AddSingleton<IProjectMetadataReader, ProjectMetadataReader>();
        services.AddSingleton<IDotNetPackInvoker, DotNetPackInvoker>();
        services.AddSingleton<IDotNetRuntimeEnvironment, DotNetRuntimeEnvironment>();
        services.AddSingleton<INuGetFeedClient, NuGetProtocolFeedClient>();
        services.AddSingleton<IMsvcDevEnvironment, MsvcDevEnvironment>();

        // VcpkgBootstrapTool is a sealed concrete (not a Cake Tool<T>) that wraps
        // bootstrap-vcpkg.bat / .sh dispatch. Tools is Cake Tool<T> wrappers ONLY,
        // so VcpkgBootstrapTool lives here in Integrations.
        services.AddSingleton<VcpkgBootstrapTool>();

        return services;
    }
}
