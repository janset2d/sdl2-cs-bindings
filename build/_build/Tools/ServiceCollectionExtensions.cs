using Microsoft.Extensions.DependencyInjection;

namespace Build.Tools;

/// <summary>
/// Composition-root grouping for Cake <see cref="Cake.Core.Tooling.Tool{TSettings}"/>
/// wrappers. The build host's <c>Program.cs</c> composes its DI graph as a single roster
/// of <c>AddXXX()</c> extension calls; this method collects the Tools-tier registrations
/// that don't belong to any one feature folder. Cake <c>Tool&lt;TSettings&gt;</c> aliases
/// (e.g. <c>VcpkgAliases</c>, <c>DumpbinAliases</c>) register themselves through Cake's
/// automatic discovery and don't need DI bindings.
/// </summary>
/// <remarks>
/// <see cref="Build.Integrations.Vcpkg.VcpkgBootstrapTool"/> lives in
/// Integrations/Vcpkg/ — it is not a Cake <c>Tool&lt;TSettings&gt;</c>
/// (sealed concrete wrapping bootstrap-vcpkg.bat/.sh dispatch).
/// </remarks>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddToolWrappers(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services;
    }
}
