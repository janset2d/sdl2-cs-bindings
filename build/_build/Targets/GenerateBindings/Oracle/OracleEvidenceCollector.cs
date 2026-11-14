using Build.Host.Cake;
using Build.Host.Paths;
using Cake.Core;
using Cake.Core.IO;

namespace Build.Targets.GenerateBindings.Oracle;

internal sealed class OracleEvidenceCollector(ICakeContext context, IPathService paths)
{
    public async Task<OracleReport> BuildReportAsync(IReadOnlyList<FamilyConfig> families)
    {
        var reports = new List<FamilyReport>();

        foreach (var family in families)
        {
            reports.Add(await BuildFamilyReportAsync(paths.RepoRoot, family));
        }

        return new OracleReport(paths.RepoRoot.FullPath, reports);
    }

    public Task WriteReportAsync(OracleReport report)
    {
        var path = paths.RepoRoot.CombineWithFilePath("spikes/binding-generators/output/reports/oracle-evidence-clangsharp.md");
        return context.WriteAllTextAsync(path, MarkdownReportRenderer.Render(report));
    }

    private async Task<FamilyReport> BuildFamilyReportAsync(DirectoryPath repoRoot, FamilyConfig config)
    {
        var compat = await CSharpEvidenceLoader.LoadPathAsync(context, repoRoot, config.ClangSharpCompatRelativePath);
        var modern = await CSharpEvidenceLoader.LoadPathAsync(context, repoRoot, config.ClangSharpModernRelativePath);
        var cake = await LoadOptionalCSharpEvidenceAsync(repoRoot, config.CakePreviewRelativePath);
        var sdl2Cs = await LoadOptionalCSharpEvidenceAsync(repoRoot, config.Sdl2CsRelativePath);

        var required = RequiredSurface.Empty;
        var dynapi = new DynapiEvidence(SourceStatus.NotApplicable, new HashSet<string>(StringComparer.Ordinal));

        if (config.UsesSdl2Dynapi)
        {
            required = await ManifestRequiredSurfaceReader.ReadAsync(context, repoRoot);
            dynapi = await DynapiEvidenceLoader.LoadAsync(context, repoRoot);
        }

        var generatedEvidence = CSharpEvidenceLoader.Combine(compat.Evidence, modern.Evidence);
        var checks = RawAbiChecks.Run(config, generatedEvidence, required);

        return new FamilyReport(
            config,
            [
                SourceReport.FromCSharp("clangsharp-compat", "ClangSharp Compat", config.ClangSharpCompatRelativePath, compat),
                SourceReport.FromCSharp("clangsharp-modern", "ClangSharp Modern", config.ClangSharpModernRelativePath, modern),
                SourceReport.FromCSharp("cake-preview", "Cake Preview", config.CakePreviewRelativePath ?? "n/a", cake),
                SourceReport.FromCSharp("sdl2-cs", "SDL2-CS", config.Sdl2CsRelativePath ?? "n/a", sdl2Cs),
                SourceReport.FromRequiredSurface("manifest-required-surface", "Manifest Required Surface", "build/manifest.json", required),
                SourceReport.FromDynapi("dynapi", "SDL2 Dynapi", "external/vcpkg or vcpkg_installed", dynapi)
            ],
            compat,
            modern,
            cake,
            sdl2Cs,
            required,
            dynapi,
            checks);
    }

    private Task<SourceEvidence> LoadOptionalCSharpEvidenceAsync(DirectoryPath repoRoot, string? relativePath)
    {
        return relativePath is null
            ? Task.FromResult(new SourceEvidence(SourceStatus.NotApplicable, new CSharpEvidence([], [], [])))
            : CSharpEvidenceLoader.LoadPathAsync(context, repoRoot, relativePath);
    }
}
