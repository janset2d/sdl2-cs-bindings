namespace Build.Results;

/// <summary>
/// Empty payload for <see cref="Result{TValue,TError}"/> when success carries no information.
/// Use as <c>Result&lt;Unit, TError&gt;</c> for "did it succeed?" with a typed failure path.
/// </summary>
public readonly record struct Unit
{
    public static Unit Value => default;
}
