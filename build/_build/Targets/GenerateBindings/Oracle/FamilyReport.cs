using Build.Results;

namespace Build.Targets.GenerateBindings.Oracle;

internal sealed record FamilyReport(
    FamilyConfig Config,
    IReadOnlyList<SourceReport> Sources,
    SourceEvidence ClangSharpCompat,
    SourceEvidence ClangSharpModern,
    SourceEvidence Sdl2Cs,
    RequiredSurface RequiredSurface,
    DynapiEvidence Dynapi,
    IReadOnlyList<RawAbiCheck> RawAbiChecks)
{
    public ValidationReport? DynapiStage1 { get; init; }
    public ValidationReport? DynapiStrict { get; init; }
    public ValidationReport? Ownership { get; init; }
    public SourceStatus OwnershipStatus { get; init; }
    public RequiredSurfaceAudit? RequiredAudit { get; init; }
}
