using Build.Harvesting;

namespace Build.Targets.Harvest.Services;

public sealed class CopierError : HarvestingError
{
    public CopierError(string message, Exception? exception = null)
        : base(message, exception)
    {
    }
}
