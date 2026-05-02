using Build.Shared.Results;

namespace Build.Features.Harvesting;

public abstract class HarvestingError : BuildError
{
    protected HarvestingError(string message, Exception? exception = null)
        : base(message, exception)
    {
    }
}
