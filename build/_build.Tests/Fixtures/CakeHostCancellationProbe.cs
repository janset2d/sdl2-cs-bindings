namespace Build.Tests.Fixtures;

internal sealed class CakeHostCancellationProbe : IDisposable
{
    private readonly SemaphoreSlim _started = new(0, 1);
    private readonly SemaphoreSlim _finish = new(0, 1);
    private bool _cancelled;

    public Task WaitForStartAsync() => _started.WaitAsync();
    public bool ObservedCancellation { get; private set; }
    public bool CleanupRan { get; private set; }

    public async Task<int> InvokeAsync(CancellationToken cancellationToken)
    {
        using var registration = cancellationToken.Register(() => _cancelled = true);
        _started.Release();

        try
        {
            await _finish.WaitAsync(CancellationToken.None);
            ObservedCancellation = _cancelled;
            return CakeHostCommandLineData.CancelledProbeExitCode;
        }
        finally
        {
            CleanupRan = true;
        }
    }

    public void Finish() => _finish.Release();

    public void Dispose()
    {
        _started.Dispose();
        _finish.Dispose();
    }
}
