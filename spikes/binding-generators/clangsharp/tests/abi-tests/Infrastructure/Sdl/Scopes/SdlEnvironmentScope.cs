namespace Janset.SDL2.AbiTests.Infrastructure.Sdl.Scopes;

internal sealed class SdlEnvironmentScope : IDisposable
{
    private readonly string _name;
    private readonly string? _previousValue;

    public SdlEnvironmentScope(string name, string value)
    {
        _name = name;
        _previousValue = Environment.GetEnvironmentVariable(name);
        Environment.SetEnvironmentVariable(name, value);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(_name, _previousValue);
    }
}
