using Build.Shared.Results;

namespace Build.Features.Preflight;

public abstract class PreflightError : BuildError
{
    protected PreflightError(string message, Exception? exception = null)
        : base(message, exception)
    {
    }
}
