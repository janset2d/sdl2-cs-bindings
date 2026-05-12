using Build.Host;
using Cake.Core;
using Cake.Core.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace Build.Tests.Fixtures;

public static class ServiceCollectionTestHost
{
    public static IServiceCollection AddFakeCakeWorld(this IServiceCollection services, FakeCakeWorld world)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(world);

        services.AddSingleton(world.CakeContext);
        services.AddSingleton<ICakeLog>(world.Log);
        services.AddSingleton(world.CakeContext.Environment);
        services.AddSingleton(world.CakeContext.FileSystem);
        services.AddSingleton(world.CakeContext.Globber);
        services.AddSingleton(world.CakeContext.Arguments);
        services.AddSingleton(world.CakeContext.Configuration);

        var buildContext = world.CreateBuildContext();
        services.AddSingleton(buildContext);
        services.AddSingleton(buildContext.Runtime);
        services.AddSingleton(buildContext.Paths);
        services.AddSingleton<IAnsiConsole>(world.AnsiConsole);

        return services;
    }
}
