using Build.Shared.Harvesting;

namespace Build.Features.Harvesting;

public sealed class CopierError : HarvestingError
{
    public CopierError(string message, Exception? exception = null)
        : base(message, exception)
    {
    }
}
