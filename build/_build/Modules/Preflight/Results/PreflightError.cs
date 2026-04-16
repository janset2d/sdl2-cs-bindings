using Build.Modules.Results;

namespace Build.Modules.Preflight.Results;

public abstract class PreflightError : BuildError
{
    protected PreflightError(string message, Exception? exception = null)
        : base(message, exception)
    {
    }
}