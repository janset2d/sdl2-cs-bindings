using System.CommandLine;

namespace Build.Tests.Fixtures;

internal sealed class CakeHostCancellationProbe
{
    private readonly TaskCompletionSource _cancelled = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _finish = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Started => _started.Task;
    public bool ObservedCancellation { get; private set; }
    public bool CleanupRan { get; private set; }

    public async Task<int> InvokeAsync(ParseResult parseResult, CancellationToken cancellationToken)
    {
        using var registration = cancellationToken.Register(_cancelled.SetResult);
        _started.SetResult();

        try
        {
            await _finish.Task;
            ObservedCancellation = _cancelled.Task.IsCompleted;
            return CakeHostCommandLineData.CancelledProbeExitCode + parseResult.Errors.Count;
        }
        finally
        {
            CleanupRan = true;
        }
    }

    public void Finish() => _finish.SetResult();
}
