using Build.Modules.Results;

namespace Build.Modules.Harvesting.Results;

public abstract class HarvestingError : BuildError
{
    protected HarvestingError(string message, Exception? exception = null)
        : base(message, exception)
    {
    }
}
