internal sealed record RequiredSurface(SourceStatus Status, IReadOnlySet<string> RequiredFunctions, IReadOnlySet<string> RequiredConstants)
{
    public static RequiredSurface Empty { get; } = new(SourceStatus.NotApplicable, EmptySet(), EmptySet());

    private static IReadOnlySet<string> EmptySet()
        => new HashSet<string>(StringComparer.Ordinal);
}
