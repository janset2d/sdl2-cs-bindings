namespace Build.Modules.Harvesting.Results;

public abstract class HarvestingError
{
    protected HarvestingError(string message, Exception? exception = null)
    {
        Message = message;
        Exception = exception;
    }

    public string Message { get; }

    public Exception? Exception { get; }
}
