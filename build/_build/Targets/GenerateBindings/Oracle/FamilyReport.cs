internal sealed record FamilyReport(
    FamilyConfig Config,
    IReadOnlyList<SourceReport> Sources,
    SourceEvidence ClangSharpCompat,
    SourceEvidence ClangSharpModern,
    SourceEvidence CakePreview,
    SourceEvidence Sdl2Cs,
    RequiredSurface RequiredSurface,
    DynapiEvidence Dynapi,
    IReadOnlyList<RawAbiCheck> RawAbiChecks);
