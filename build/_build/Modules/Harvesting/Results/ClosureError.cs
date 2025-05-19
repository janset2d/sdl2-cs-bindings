namespace Build.Modules.Harvesting.Results;

public class ClosureError : HarvestingError
{
    public ClosureError(string message, Exception? exception = null)
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
