using Build.Domain.Results;

namespace Build.Domain.Preflight.Results;

public abstract class PreflightError : BuildError
{
    protected PreflightError(string message, Exception? exception = null)
        : base(message, exception)
    {
    }
}