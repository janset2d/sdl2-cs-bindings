using Microsoft.Extensions.DependencyInjection;

namespace Build.Host.Clock;

internal static class SystemClockRegistration
{
    internal static IServiceCollection AddSystemClock(this IServiceCollection services) => services.AddSingleton(TimeProvider.System);
}
