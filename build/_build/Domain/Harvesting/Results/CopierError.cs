namespace Build.Domain.Harvesting.Results;

public sealed class CopierError : HarvestingError
{
    public CopierError(string message, Exception? exception = null)
        : base(message, exception)
    {
    }
}
