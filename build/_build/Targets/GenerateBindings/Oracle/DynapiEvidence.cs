namespace Build.Targets.GenerateBindings.Oracle;

internal sealed record DynapiEvidence(SourceStatus Status, IReadOnlySet<string> Exports);
