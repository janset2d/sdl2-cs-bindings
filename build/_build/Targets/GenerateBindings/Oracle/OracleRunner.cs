internal static class OracleRunner
{
    private static readonly FamilyConfig[] KnownFamilies =
    [
        FamilyConfigs.Sdl2Core,
        FamilyConfigs.Sdl2Image,
        FamilyConfigs.Sdl2Ttf,
        FamilyConfigs.Sdl2Mixer,
        FamilyConfigs.Sdl2Gfx,
    ];

    public static int Run(OracleOptions options)
    {
        var configs = new List<FamilyConfig>();
        foreach (var family in options.Families)
        {
            var config = KnownFamilies.FirstOrDefault(known => known.FamilyId.Equals(family, StringComparison.OrdinalIgnoreCase));
            if (config is null)
            {
                Console.Error.WriteLine($"Unknown family: {family}");
                return 2;
            }

            configs.Add(config);
        }

        var familyReports = configs.Select(config => BuildFamilyReport(options.RepoRoot, config)).ToArray();

        if (options.Stdout)
        {
            foreach (var report in familyReports)
            {
                WriteFamilyStatus(options.RepoRoot, report);
            }
        }

        if (options.WriteReport)
        {
            var report = new OracleReport(
                options.RepoRoot,
                familyReports);
            var reportPath = Path.Combine(
                options.RepoRoot,
                "spikes",
                "binding-generators",
                "output",
                "reports",
                "oracle-evidence-clangsharp.md");
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
            File.WriteAllText(reportPath, MarkdownReportRenderer.Render(report));
            Console.WriteLine($"Report: {reportPath}");
        }

        return 0;
    }

    private static FamilyReport BuildFamilyReport(string repoRoot, FamilyConfig config)
    {
        var compat = CSharpEvidenceLoader.LoadPath(repoRoot, config.ClangSharpCompatRelativePath);
        var modern = CSharpEvidenceLoader.LoadPath(repoRoot, config.ClangSharpModernRelativePath);
        var cake = LoadOptionalCSharpEvidence(repoRoot, config.CakePreviewRelativePath);
        var sdl2Cs = LoadOptionalCSharpEvidence(repoRoot, config.Sdl2CsRelativePath);

        var required = RequiredSurface.Empty;
        var dynapi = new DynapiEvidence(SourceStatus.NotApplicable, new HashSet<string>(StringComparer.Ordinal));
        if (config.UsesSdl2Dynapi)
        {
            required = ManifestRequiredSurfaceReader.Read(repoRoot);
            dynapi = DynapiEvidenceLoader.Load(repoRoot);
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

    private static void WriteFamilyStatus(string repoRoot, FamilyReport report)
    {
        Console.WriteLine($"Family: {report.Config.FamilyId} ({report.Config.DisplayName})");
        Console.WriteLine($"  Expected namespace: {report.Config.ExpectedNamespace}");
        Console.WriteLine($"  Expected raw class: {report.Config.ExpectedRawClassName}");
        Console.WriteLine($"  ClangSharp Compat: {FormatCSharpEvidence(report.ClangSharpCompat)}");
        Console.WriteLine($"  ClangSharp Modern: {FormatCSharpEvidence(report.ClangSharpModern)}");
        Console.WriteLine($"  Cake preview: {FormatCSharpEvidence(report.CakePreview)}");
        Console.WriteLine($"  SDL2-CS: {FormatCSharpEvidence(report.Sdl2Cs)}");
        Console.WriteLine($"  Manifest required surface: {FormatRequiredSurface(report.RequiredSurface)}");
        Console.WriteLine($"  Dynapi: {FormatDynapiEvidence(report.Dynapi)}");
        WriteRawAbiChecks(repoRoot, report.RawAbiChecks);
    }

    private static void WriteRawAbiChecks(string repoRoot, IReadOnlyList<RawAbiCheck> checks)
    {
        Console.WriteLine($"  Raw ABI checks: {checks.Count}");
        foreach (var check in RawAbiCheckSampler.Sample(checks, 8))
        {
            Console.WriteLine($"    - {check.CheckId} [{check.Severity}/{check.Classification}] {check.Symbol}: {check.Message} ({MarkdownReportRenderer.FormatReportPath(repoRoot, check.SourcePath)})");
        }

        if (checks.Count > 8)
        {
            Console.WriteLine($"    ... {checks.Count - 8} more");
        }
    }

    private static SourceEvidence LoadOptionalCSharpEvidence(string repoRoot, string? relativePath)
        => relativePath is null
            ? new SourceEvidence(SourceStatus.NotApplicable, new CSharpEvidence([], [], []))
            : CSharpEvidenceLoader.LoadPath(repoRoot, relativePath);

    private static string FormatCSharpEvidence(SourceEvidence source)
        => source.Status switch
        {
            SourceStatus.Present => $"present (functions: {source.Evidence.Functions.Count}, constants: {source.Evidence.Constants.Count}, types: {source.Evidence.Types.Count})",
            SourceStatus.Missing => "missing (functions: 0, constants: 0, types: 0)",
            SourceStatus.NotApplicable => "not-applicable",
            SourceStatus.OutOfScope => "out-of-scope/not-applicable",
            _ => source.Status.ToString()
        };

    private static string FormatRequiredSurface(RequiredSurface surface)
        => surface.Status switch
        {
            SourceStatus.Present => $"present (functions: {surface.RequiredFunctions.Count}, constants: {surface.RequiredConstants.Count})",
            SourceStatus.Missing => "missing (functions: 0, constants: 0)",
            _ => surface.Status.ToString()
        };

    private static string FormatDynapiEvidence(DynapiEvidence evidence)
        => evidence.Status switch
        {
            SourceStatus.Present => $"present (exports: {evidence.Exports.Count})",
            SourceStatus.Missing => "missing (exports: 0)",
            _ => evidence.Status.ToString()
        };
}
