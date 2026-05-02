using Build.Shared.Results;

namespace Build.Shared.Harvesting;

public abstract class HarvestingError : BuildError
{
    protected HarvestingError(string message, Exception? exception = null)
        : base(message, exception)
    {
    }
}
