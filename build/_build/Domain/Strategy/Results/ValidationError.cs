using Build.Domain.Harvesting.Models;
using Build.Domain.Results;

namespace Build.Domain.Strategy.Results;

/// <summary>
/// Base class for packaging strategy validation errors.
/// </summary>
public abstract class StrategyError : BuildError
{
    protected StrategyError(string message, Exception? exception = null)
        : base(message, exception)
    {
    }
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

public sealed class StrategyResolutionError : StrategyError
{
    public StrategyResolutionError(string message, Exception? exception = null)
        : base(message, exception)
    {
    }
}
