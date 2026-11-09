using Build.Tests.Fixtures.RepositoryGates;

namespace Build.Tests.Fixtures.QualityGates;

public sealed class SuppressionLedgerSession
{
    private readonly Lazy<(IReadOnlyList<string> Entries, string Baseline, string[] Added, string[] Stale)> _snapshot = new(ReadTree);

    public IReadOnlyList<string> Entries => _snapshot.Value.Entries;
    public string Baseline => _snapshot.Value.Baseline;
    public IReadOnlyList<string> Added => _snapshot.Value.Added;
    public IReadOnlyList<string> Stale => _snapshot.Value.Stale;

    private static (IReadOnlyList<string> Entries, string Baseline, string[] Added, string[] Stale) ReadTree()
    {
        var entries = SuppressionLedger.ReadTracked();
        var actual = SuppressionLedger.BaselineEntries(SuppressionLedger.Render(entries)).ToArray();
        var baseline = RepositoryFiles.ReadText(SuppressionLedger.BaselinePath) ?? throw new FileNotFoundException(SuppressionLedger.BaselinePath);
        var expected = SuppressionLedger.BaselineEntries(baseline).ToArray();
        return (entries, baseline, actual.Except(expected, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            expected.Except(actual, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
    }
}
