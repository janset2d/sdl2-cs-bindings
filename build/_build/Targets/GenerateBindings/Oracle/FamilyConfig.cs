namespace Build.Targets.GenerateBindings.Oracle;

internal sealed record FamilyConfig(
    string FamilyId,
    string DisplayName,
    string ExpectedNamespace,
    string ExpectedRawClassName,
    string? CakePreviewRelativePath,
    string ClangSharpCompatRelativePath,
    string ClangSharpModernRelativePath,
    string? Sdl2CsRelativePath,
    bool UsesSdl2Dynapi);
