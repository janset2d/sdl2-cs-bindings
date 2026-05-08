using Build.Targets.PreFlightCheck.Reporting;
using Microsoft.Extensions.DependencyInjection;

namespace Build.Targets.PreFlightCheck;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers PreFlightCheck-specific collaborators. Validators are registered once
    /// via <c>Build.Validation.ServiceCollectionExtensions.AddValidators()</c>; this
    /// extension only wires the target's local reporter. The Cake task itself is
    /// discovered from <see cref="Cake.Frosting.TaskNameAttribute"/> and is not
    /// explicitly registered here.
    /// </summary>
    public static IServiceCollection AddPreFlightCheck(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<PreflightReporter>();
        return services;
    }
}
