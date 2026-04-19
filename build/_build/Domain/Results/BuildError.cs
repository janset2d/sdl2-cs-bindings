namespace Build.Domain.Results;

/// <summary>
/// Cross-cutting base type for build-host domain errors.
/// Keeps the shared <c>Message</c> + optional <c>Exception</c> shape in one place
/// while allowing modules to expose more specific derived error types.
/// </summary>
public abstract class BuildError
{
    protected BuildError(string message, Exception? exception = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        Message = message;
        Exception = exception;
    }

    public string Message { get; }

    public Exception? Exception { get; }

    public override string ToString() => Message;
}