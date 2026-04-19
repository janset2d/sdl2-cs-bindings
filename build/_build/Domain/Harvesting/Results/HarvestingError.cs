using Build.Domain.Results;

namespace Build.Domain.Harvesting.Results;

public abstract class HarvestingError : BuildError
{
    protected HarvestingError(string message, Exception? exception = null)
        : base(message, exception)
    {
    }
}
