namespace Build.Tests.Fixtures.RepositoryGates;

internal sealed record GateBaseline(IReadOnlySet<string> Entries)
{
    public static GateBaseline Load(string fileName) => new(RepositoryFiles
        .ReadText("build/_build.Tests/Integration/RepositoryGates/" + fileName).Split('\n')
        .Select(line => line.Trim()).Where(line => line.Length > 0 && !line.StartsWith('#'))
        .ToHashSet(StringComparer.Ordinal));
}
