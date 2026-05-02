namespace Build.Shared.Results;

/// <summary>
/// Void marker for <see cref="OneOf.Monads.Result{TError, TSuccess}"/> when the success
/// branch carries no payload. Lives in <c>Shared/Results/</c> as a cross-feature primitive
/// per ADR-004 §2.6.1; consumed by Result types in any feature folder
/// (e.g. <c>CopierResult</c>, <c>DotNetPackResult</c>).
/// </summary>
public readonly struct Unit
{
    public static readonly Unit Value;
}
