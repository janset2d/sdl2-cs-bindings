namespace Build.Targets.PackageConsumerSmoke.Services;
public sealed record ScopeComparison(IReadOnlyList<string> Missing, IReadOnlyList<string> Unexpected)
{
    public bool IsMatch => Missing.Count == 0 && Unexpected.Count == 0;
}
