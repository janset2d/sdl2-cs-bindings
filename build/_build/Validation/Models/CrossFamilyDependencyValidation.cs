
namespace Build.Validation.Models;
/// <summary>
/// Aggregate of per-dependency cross-family checks emitted by
/// <c>ICrossFamilyDependencyResolvabilityValidator.Validate</c>. Release-guardrails [G58].
/// </summary>
public sealed record CrossFamilyDependencyValidation(IReadOnlyList<CrossFamilyDependencyCheck> Checks)
{
    public bool HasErrors => Checks.Any(check => check.IsError);
}
