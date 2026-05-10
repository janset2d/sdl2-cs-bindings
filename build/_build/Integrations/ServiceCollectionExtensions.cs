using Build.Integrations.DotNet;
using Build.Integrations.NuGet;
using Build.Shared.Runtime;
using Build.Tools;
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

        services.AddSingleton<IProjectMetadataReader, ProjectMetadataReader>();
        // IDotNetPackInvoker relocated target-local to Targets/Package/Services/ in S13;
        // registration lives in Targets/Package/ServiceCollectionExtensions.AddPackage().
        services.AddSingleton<IDotNetRuntimeEnvironment, DotNetRuntimeEnvironment>();
        services.AddSingleton<INuGetFeedClient, NuGetProtocolFeedClient>();

        // VcpkgBootstrapTool relocated to Tools/ in S15 (P9). Vcpkg integration types
        // (IPackageInfoProvider, IVcpkgManifestReader) moved to root Build.Vcpkg/
        // namespace and register through AddVcpkg() in the composition root.
        services.AddSingleton<VcpkgBootstrapTool>();

        return services;
    }
}
