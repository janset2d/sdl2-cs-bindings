using Microsoft.Extensions.DependencyInjection;

namespace Build.Tests.Fixtures;

internal sealed class CakeHostRunProbe
{
    public bool WasInvoked { get; private set; }
    public string[] Arguments { get; private set; } = [];
    public Action<IServiceCollection>? ConfigureServices { get; private set; }

    public int Run(string[] args, Action<IServiceCollection> configureServices)
    {
        WasInvoked = true;
        Arguments = args;
        ConfigureServices = configureServices;
        return ProgramCompositionRootData.ProbeExitCode;
    }
}
