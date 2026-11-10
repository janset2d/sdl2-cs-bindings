using Microsoft.Extensions.DependencyInjection;

namespace Build.Tests.Fixtures;

internal sealed class CakeHostRunProbe
{
    public string[] Arguments { get; private set; } = [];
    public Action<IServiceCollection>? ConfigureServices { get; private set; }

    public int Run(string[] args, Action<IServiceCollection> configureServices)
    {
        Arguments = args;
        ConfigureServices = configureServices;
        return ProgramCompositionRootData.ProbeExitCode;
    }
}
