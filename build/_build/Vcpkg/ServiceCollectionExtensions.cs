using Microsoft.Extensions.DependencyInjection;

namespace Build.Vcpkg;

/// <summary>
/// Vcpkg root named concept (ADR-002 §7) — promoted from Integrations/Vcpkg/ in S15 (P9).
/// Vcpkg is a cross-target concept consumed by Harvest (BinaryClosureWalker) and PreFlightCheck;
/// the root location reflects that. Tools/VcpkgBootstrapTool.cs lives separately because it is
/// a Cake Tool&lt;TSettings&gt;-style wrapper.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddVcpkg(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IPackageInfoProvider, VcpkgCliProvider>();
        services.AddSingleton<IVcpkgManifestReader, VcpkgManifestReader>();

        return services;
    }
}
