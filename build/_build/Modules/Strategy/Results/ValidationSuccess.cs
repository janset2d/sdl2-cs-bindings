using Build.Modules.Harvesting.Models;
using Build.Modules.Strategy.Models;

namespace Build.Modules.Strategy.Results;

/// <summary>
/// Represents a successful packaging policy validation.
/// <para>
/// In <see cref="ValidationMode.Warn"/> mode, the validation passes (non-blocking)
/// but <see cref="Warnings"/> contains the nodes that would have been violations
/// in Strict mode. The caller should log these for visibility.
/// </para>
/// </summary>
public sealed record ValidationSuccess
{
    /// <summary>
    /// The validation mode that produced this result.
    /// </summary>
    public ValidationMode Mode { get; }

    /// <summary>
    /// Whether any warnings were produced. <c>true</c> when violations exist
    /// but the mode is <see cref="ValidationMode.Warn"/> (non-blocking).
    /// </summary>
    public bool HasWarnings => Warnings.Count > 0;

    /// <summary>
    /// Binary nodes that would be violations in Strict mode but are treated as
    /// non-blocking warnings. Empty in Strict and Off modes.
    /// </summary>
    public IReadOnlyList<BinaryNode> Warnings { get; }

    public ValidationSuccess(ValidationMode mode, IReadOnlyList<BinaryNode>? warnings = null)
    {
        Mode = mode;
        Warnings = warnings is not null ? [.. warnings] : []; // Defensive copy
    }
}
