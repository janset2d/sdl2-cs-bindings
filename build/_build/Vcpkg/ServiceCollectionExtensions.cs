using Microsoft.Extensions.DependencyInjection;

namespace Build.Vcpkg;

/// <summary>
/// Vcpkg root named concept (ADR-002 §7) — promoted from Integrations/Vcpkg/ in S15 (P9).
/// Vcpkg is a cross-target concept consumed by Harvest (BinaryClosureWalker) and PreFlightCheck;
/// the root location reflects that. After the P10 absorb (S16), the manifest-load surface is
/// owned end-to-end by <see cref="Build.Repositories.VcpkgManifestRepository"/>; this module
/// only registers the vcpkg CLI provider. <c>VcpkgBootstrapTool</c> registration moved to
/// <c>Build.Tools.ServiceCollectionExtensions.AddToolWrappers()</c> alongside the other Cake
/// <c>Tool&lt;TSettings&gt;</c> wrappers.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddVcpkg(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IPackageInfoProvider, VcpkgCliProvider>();

        return services;
    }
}
