using Microsoft.Extensions.DependencyInjection;

namespace Build.Tools;

/// <summary>
/// Composition-root grouping for Cake <see cref="Cake.Core.Tooling.Tool{TSettings}"/>
/// wrappers. The build host's <c>Program.cs</c> composes its DI graph as a single roster
/// of <c>AddXXX()</c> extension calls; this method collects the Tools-tier registrations
/// that don't belong to any one target. Cake <c>Tool&lt;TSettings&gt;</c> aliases
/// (e.g. <c>VcpkgAliases</c>, <c>DumpbinAliases</c>) register themselves through Cake's
/// automatic discovery and don't need DI bindings.
/// </summary>
/// <remarks>
/// <see cref="VcpkgBootstrapTool"/> is a sealed concrete wrapper (not a
/// Cake <c>Tool&lt;TSettings&gt;</c>) for the <c>bootstrap-vcpkg.bat</c> / <c>bootstrap-vcpkg.sh</c>
/// dispatch; it ships in Tools/ alongside the rest of the tool-tier wrappers.
/// </remarks>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddToolWrappers(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<VcpkgBootstrapTool>();

        return services;
    }
}
