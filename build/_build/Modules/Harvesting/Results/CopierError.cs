namespace Build.Modules.Harvesting.Results;

public sealed class CopierError : ClosureError
{
    public CopierError(string message, Exception? exception = null)
        : base(message, exception)
    {
    }
}
