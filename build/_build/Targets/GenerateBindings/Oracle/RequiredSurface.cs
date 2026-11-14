namespace Build.Targets.GenerateBindings.Oracle;

internal sealed record RequiredSurface(SourceStatus Status, IReadOnlySet<string> RequiredFunctions, IReadOnlySet<string> RequiredConstants)
{
    public static RequiredSurface Empty { get; } = new(SourceStatus.NotApplicable, EmptySet(), EmptySet());

    private static HashSet<string> EmptySet()
        => [with(StringComparer.Ordinal)];
}
