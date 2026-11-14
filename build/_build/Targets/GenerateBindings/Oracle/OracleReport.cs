namespace Build.Targets.GenerateBindings.Oracle;

internal sealed record OracleReport(string RepoRoot, IReadOnlyList<FamilyReport> Families);
