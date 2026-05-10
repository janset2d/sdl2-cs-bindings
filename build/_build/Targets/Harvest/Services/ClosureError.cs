using Build.Harvesting;

namespace Build.Targets.Harvest.Services;

public abstract class ClosureError : HarvestingError
{
    protected ClosureError(string message, Exception? exception = null)
        : base(message, exception)
    {
    }
}

public sealed class ClosureNotFound : ClosureError
{
    public ClosureNotFound(string message) : base(message)
    {
    }
}

public sealed class ClosureBuildError : ClosureError
{
    public ClosureBuildError(string message, Exception? exception = null)
        : base(message, exception)
    {
    }
}
