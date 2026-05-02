using Build.Tools.Vcpkg;
using Microsoft.Extensions.DependencyInjection;

namespace Build.Tools;

/// <summary>
/// Composition-root grouping for Cake <see cref="Cake.Core.Tooling.Tool{TSettings}"/>
/// wrappers (ADR-004 §2.10). Per ADR-004 §2.12 the build host's <c>Program.cs</c>
/// composes its DI graph as a single roster of <c>AddXXX()</c> extension calls;
/// this method collects the Tools-tier registrations that don't belong to any one
/// feature folder. Cake `Tool<T>` aliases (e.g. <c>VcpkgAliases</c>, <c>DumpbinAliases</c>)
/// register themselves through Cake's automatic discovery and don't need DI bindings.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddToolWrappers(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // VcpkgBootstrapTool is a sealed concrete (not a Cake Tool<T>) that wraps
        // bootstrap-vcpkg.bat / .sh dispatch. Phase-x §2.7 Adım-13 follow-up note
        // flags candidacy for Integrations/Vcpkg/ relocation in a later wave.
        services.AddSingleton<VcpkgBootstrapTool>();

        return services;
    }
}
