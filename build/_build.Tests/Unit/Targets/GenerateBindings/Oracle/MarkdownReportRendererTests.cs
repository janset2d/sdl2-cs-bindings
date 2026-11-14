using Build.Targets.GenerateBindings.Oracle;
using Build.Tests.Fixtures;

namespace Build.Tests.Unit.Targets.GenerateBindings.Oracle;

public sealed class MarkdownReportRendererTests
{
    [Test]
    public async Task Render_Should_Preserve_The_Complete_Report_Sections_Classifications_Native_Names_And_Lf_Bytes()
    {
        var report = CreateReport();
        var markdown = MarkdownReportRenderer.Render(report);

        await Assert.That(markdown).IsEqualTo(FixtureLoader.Load("BindingGeneration/Oracle/renderer.md.txt"));
        await Assert.That(markdown).DoesNotContain("ManagedHelper");
        await Assert.That(markdown).DoesNotContain("INTERNAL_SDL_Init");
        await Assert.That(markdown).DoesNotContain(report.RepoRoot);
        await Assert.That(markdown).DoesNotContain("Generated UTC");
        await Assert.That(markdown).DoesNotContain('\r');
    }

    [Test]
    public async Task FormatReportPath_Should_Normalize_Repository_Local_Source_Paths()
    {
        var root = Path.GetFullPath("oracle-repo");

        await Assert.That(MarkdownReportRenderer.FormatReportPath(root, Path.Combine(root, "src", "core.g.cs"))).IsEqualTo("src/core.g.cs");
    }

    private static OracleReport CreateReport()
    {
        var repoRoot = Path.GetFullPath("oracle-repo");
        var corePath = Path.Combine(repoRoot, "src", "core.g.cs");
        var imagePath = Path.Combine(repoRoot, "src", "image.g.cs");
        var core = CSharpEvidenceExtractor.Extract(corePath, FixtureLoader.Load("BindingGeneration/Oracle/mixed-generated.cs.txt"), ["NET5_0_OR_GREATER"]);
        var image = CSharpEvidenceExtractor.Extract(imagePath, FixtureLoader.Load("BindingGeneration/Oracle/image-namespace-drift.cs.txt"), ["NET5_0_OR_GREATER"]);
        var entryPoint = CSharpEvidenceExtractor.Extract(Path.Combine(repoRoot, "src", "sdl2-cs.cs"),
            FixtureLoader.Load("BindingGeneration/Oracle/entrypoint-import.cs.txt"), []);
        var absent = new SourceEvidence(SourceStatus.NotApplicable, new CSharpEvidence([], [], []));
        var imageReport = new FamilyReport(
            FamilyConfigs.Sdl2Image,
            [new SourceReport("clangsharp-compat", "ClangSharp Compat", imagePath, SourceStatus.Present, 1, 0, 1)],
            new SourceEvidence(SourceStatus.Present, image), absent, absent, absent, RequiredSurface.Empty,
            new DynapiEvidence(SourceStatus.NotApplicable, new HashSet<string>(StringComparer.Ordinal)),
            [new RawAbiCheck("family-namespace-drift", "error", "Hard Bug", "SDL2", "Generated namespace should be SDL2.Image.", imagePath)]);
        var coreReport = new FamilyReport(
            FamilyConfigs.Sdl2Core,
            [
                new SourceReport("clangsharp-compat", "ClangSharp Compat", corePath, SourceStatus.Present, 5, 7, 6),
                new SourceReport("cake-preview", "Cake Preview", "missing", SourceStatus.Missing, 0, 0, 0)
            ],
            new SourceEvidence(SourceStatus.Present, core), new SourceEvidence(SourceStatus.Present, entryPoint),
            new SourceEvidence(SourceStatus.Missing, new CSharpEvidence([], [], [])), absent,
            new RequiredSurface(SourceStatus.Present, new HashSet<string>(["SDL_MissingFunction"], StringComparer.Ordinal),
                new HashSet<string>(["SDL_MISSING_CONSTANT"], StringComparer.Ordinal)),
            new DynapiEvidence(SourceStatus.Present, new HashSet<string>(["SDL_Init"], StringComparer.Ordinal)),
            [
                new RawAbiCheck("raw-abi-public-import", "error", "Hard Bug", "SDL_Init", "Raw native import method is public.", corePath),
                new RawAbiCheck("required-function-missing", "error", "Evidence Missing", "SDL_MissingFunction", "Required function is missing.", "manifest")
            ]);

        return new OracleReport(repoRoot, [imageReport, coreReport]);
    }
}
