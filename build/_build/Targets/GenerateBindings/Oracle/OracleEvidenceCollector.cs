using Build.Data.BindingGeneration;
using Build.Data.BindingGeneration.Models;
using Build.Host.Cake;
using Build.Host.Paths;
using Build.Results;
using Build.Validation.BindingGeneration;
using Cake.Core;
using Cake.Core.IO;

namespace Build.Targets.GenerateBindings.Oracle;

internal sealed class OracleEvidenceCollector(ICakeContext context, IPathService paths, IDynapiManifestRepository dynapiRepository)
{
    public async Task<Result<OracleReport, ManifestResolutionError>> BuildReportAsync(IReadOnlyList<BindingGenerationConfig> families, CancellationToken ct = default)
    {
        var reports = new List<FamilyReport>();

        foreach (var family in families)
        {
            ct.ThrowIfCancellationRequested();
            var result = await BuildFamilyReportAsync(paths.RepoRoot, FamilyConfig.FromProfile(family), ct);

            if (result.TryGetError(out var error))
            {
                return Result<OracleReport, ManifestResolutionError>.Failure(error);
            }

            reports.Add(result.Value);
        }

        return Result<OracleReport, ManifestResolutionError>.Success(new OracleReport(paths.RepoRoot.FullPath, reports));
    }

    public Task WriteReportAsync(OracleReport report)
    {
        var path = paths.RepoRoot.CombineWithFilePath("artifacts/binding-generation/oracle-evidence.md");
        return context.WriteAllTextAsync(path, MarkdownReportRenderer.Render(report));
    }

    private async Task<Result<FamilyReport, ManifestResolutionError>> BuildFamilyReportAsync(DirectoryPath repoRoot, FamilyConfig config, CancellationToken ct)
    {
        var compat = await CSharpEvidenceLoader.LoadPathAsync(context, repoRoot, config.ClangSharpCompatRelativePath);
        var modern = await CSharpEvidenceLoader.LoadPathAsync(context, repoRoot, config.ClangSharpModernRelativePath);
        var sdl2Cs = await LoadOptionalCSharpEvidenceAsync(repoRoot, config.Sdl2CsRelativePath);

        var required = RequiredSurface.Empty;
        var dynapi = new DynapiEvidence(SourceStatus.NotApplicable, new HashSet<string>(StringComparer.Ordinal));
        ValidationReport? stage1 = null;
        ValidationReport? strict = null;

        if (config.UsesSdl2Dynapi)
        {
            required = new RequiredSurface(SourceStatus.Present,
                config.Profile.RequiredFunctions.Select(function => function.Name).ToHashSet(StringComparer.Ordinal),
                config.Profile.RequiredConstants.Select(constant => constant.Name).ToHashSet(StringComparer.Ordinal));
            var result = await dynapiRepository.LoadAsync(ct);

            if (result.TryGetError(out var error))
            {
                if (error.Kind != ManifestResolutionErrorKind.NotFound)
                {
                    return Result<FamilyReport, ManifestResolutionError>.Failure(error);
                }

                dynapi = new DynapiEvidence(SourceStatus.Missing, new HashSet<string>(StringComparer.Ordinal));
            }
            else
            {
                dynapi = new DynapiEvidence(SourceStatus.Present, result.Value.PublicSymbols);
                var emitted = CSharpEvidenceLoader.Combine(compat.Evidence, modern.Evidence).Functions
                    .Where(function => function.IsNativeImportDeclaration)
                    .Select(function => function.NativeEntryPoint)
                    .ToHashSet(StringComparer.Ordinal);
                var validator = new DynapiCoherenceValidator(dynapiRepository);
                stage1 = await validator.ValidateAsync(emitted, config.Profile, SeverityProfile.Stage1Generator, ct);
                strict = await validator.ValidateAsync(emitted, config.Profile, SeverityProfile.Stage2Strict, ct);
            }
        }

        var generatedEvidence = CSharpEvidenceLoader.Combine(compat.Evidence, modern.Evidence);
        var checks = RawAbiChecks.Run(config, generatedEvidence, required);
        var owned = await CSharpEvidenceLoader.LoadPathAsync(context, repoRoot, config.Profile.GeneratedRoot);

        return Result<FamilyReport, ManifestResolutionError>.Success(new FamilyReport(
            config,
            [
                SourceReport.FromCSharp("clangsharp-compat", "ClangSharp Compat", config.ClangSharpCompatRelativePath, compat),
                SourceReport.FromCSharp("clangsharp-modern", "ClangSharp Modern", config.ClangSharpModernRelativePath, modern),
                SourceReport.FromCSharp("sdl2-cs", "SDL2-CS", config.Sdl2CsRelativePath ?? "n/a", sdl2Cs),
                SourceReport.FromRequiredSurface("manifest-required-surface", "Manifest Required Surface", "build/manifest.json", required),
                SourceReport.FromDynapi("dynapi", "SDL2 Dynapi", "external/vcpkg or vcpkg_installed", dynapi)
            ],
            compat,
            modern,
            sdl2Cs,
            required,
            dynapi,
            checks)
        {
            DynapiStage1 = stage1,
            DynapiStrict = strict,
            Ownership = OwnershipAudit.Run(owned.Evidence, config.Profile),
            OwnershipStatus = owned.Status,
            RequiredAudit = config.UsesSdl2Dynapi ? RequiredSurfaceAudit.Run(config.Profile, compat, modern) : null
        });
    }

    private Task<SourceEvidence> LoadOptionalCSharpEvidenceAsync(DirectoryPath repoRoot, string? relativePath)
    {
        return relativePath is null
            ? Task.FromResult(new SourceEvidence(SourceStatus.NotApplicable, new CSharpEvidence([], [], [])))
            : CSharpEvidenceLoader.LoadPathAsync(context, repoRoot, relativePath);
    }
}
