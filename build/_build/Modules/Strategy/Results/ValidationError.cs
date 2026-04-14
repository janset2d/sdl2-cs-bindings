using Build.Modules.Harvesting.Models;

namespace Build.Modules.Strategy.Results;

/// <summary>
/// Base class for packaging strategy validation errors.
/// </summary>
public abstract class StrategyError
{
    protected StrategyError(string message, Exception? exception = null)
    {
        Message = message;
        Exception = exception;
    }

    /// <summary>Human-readable error description.</summary>
    public string Message { get; }

    /// <summary>Optional underlying exception.</summary>
    public Exception? Exception { get; }
}

/// <summary>
/// Represents a packaging policy validation failure in Strict mode.
/// Contains the binary nodes that violated the policy (transitive dep leaks).
/// </summary>
public sealed class ValidationError : StrategyError
{
    /// <summary>
    /// The binary nodes that violated the packaging policy.
    /// </summary>
    public IReadOnlyList<BinaryNode> Violations { get; }

    public ValidationError(string message, IReadOnlyList<BinaryNode> violations, Exception? exception = null)
        : base(message, exception)
    {
        Violations = [.. violations]; // Defensive copy
    }
}
