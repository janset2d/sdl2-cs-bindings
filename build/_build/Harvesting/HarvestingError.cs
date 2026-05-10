using Build.Results;

namespace Build.Harvesting;

public abstract class HarvestingError : BuildError
{
    protected HarvestingError(string message, Exception? exception = null)
        : base(message, exception)
    {
    }
}
