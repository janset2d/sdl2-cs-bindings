using Build.Host;
using Build.Host.Cake;
using Cake.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Build.DependencyAnalysis;

/// <summary>
/// Cross-target named concept (ADR-002 §7) for binary dependency analysis. Owns the host-platform
/// dispatch closure for <see cref="IRuntimeScanner"/>: the host RID determines which native
/// inspector implementation answers a scan (<c>dumpbin</c> on Windows, <c>ldd</c> on Linux,
/// <c>otool</c> on macOS). Today's single production consumer is the harvest pipeline's
/// <c>BinaryClosureWalker</c>; the root location reflects that scanners are a host-platform
/// abstraction, not a Harvest-domain detail.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDependencyAnalysis(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IRuntimeScanner>(provider =>
        {
            var env = provider.GetRequiredService<ICakeEnvironment>();
            var context = provider.GetRequiredService<ICakeContext>();

            var currentRid = env.Platform.Rid();
            return currentRid switch
            {
                Rids.WinX64 or Rids.WinX86 or Rids.WinArm64 => new WindowsDumpbinScanner(context),
                Rids.LinuxX64 or Rids.LinuxArm64 => new LinuxLddScanner(context),
                Rids.OsxX64 or Rids.OsxArm64 => new MacOtoolScanner(context),
                _ => throw new NotSupportedException($"Unsupported OS for IRuntimeScanner: {currentRid}"),
            };
        });

        return services;
    }
}
