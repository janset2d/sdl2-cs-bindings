internal static class SelfTests
{
    public static int Run()
    {
        var failures = new List<string>();
        var evidence = CSharpEvidenceExtractor.Extract("fixture.g.cs", Fixtures.MixedGeneratedSource, ["NET5_0_OR_GREATER"]);

        Expect(evidence.Functions.Count == 5, "extracts DllImport, LibraryImport, scoped const-backed DllImports, and non-import methods", failures);
        Expect(evidence.Functions.Any(f => f.ManagedName == "SDL_Init" && f.NativeLibrary == "SDL2" && f.ImportKind == "DllImport"), "extracts DllImport library and method", failures);
        Expect(evidence.Functions.Any(f => f.ManagedName == "IMG_Init" && f.NativeLibrary == "SDL2_image" && f.ImportKind == "LibraryImport"), "extracts LibraryImport library and method", failures);
        Expect(evidence.Functions.Any(f => f.ManagedName == "SDL_Quit" && f.NativeLibrary == "SDL2" && f.ImportKind == "DllImport"), "extracts const-backed DllImport library names", failures);
        Expect(evidence.Functions.Any(f => f.ManagedName == "SDL_Quit" && f.NamespaceName == "SDL2.Image"), "extracts nested block namespace names", failures);
        Expect(evidence.Functions.Any(f => f.ManagedName == "IMG_Quit" && f.NativeLibrary == "SDL2_image" && f.ImportKind == "DllImport"), "resolves duplicate const-backed import names from the containing type scope", failures);
        Expect(evidence.Constants.Any(c => c.Name == "SDL_INIT_VIDEO" && c.Kind == "Field"), "extracts const fields", failures);
        Expect(evidence.Constants.Any(c => c.Name == "SDL_INIT_EVERYTHING" && c.Kind == "Field"), "extracts const computed required constants", failures);
        Expect(evidence.Constants.Any(c => c.Name == "SDL_HINT_RENDER_DRIVER" && c.Kind == "Property"), "extracts expression-bodied UTF-8 span properties", failures);
        Expect(evidence.Types.Any(t => t.Name == "SDL_RWops" && t.Kind == "Struct"), "extracts structs", failures);
        Expect(evidence.Types.Any(t => t.Name == "SDL_bool" && t.Kind == "Enum"), "extracts enums", failures);

        Expect(DynapiParser.Parse(Fixtures.DynapiExports).Contains("SDL_Init"), "parses SDL2 dynapi exports", failures);

        var required = ManifestRequiredSurfaceReader.ReadJson(Fixtures.ManifestRequiredSurface);
        Expect(required.RequiredFunctions.Contains("SDL_Init"), "parses manifest required SDL.h functions", failures);
        Expect(required.RequiredConstants.Contains("SDL_INIT_VIDEO"), "parses manifest required SDL.h constants", failures);

        var sdl2Cs = CSharpEvidenceExtractor.Extract("SDL2.cs", Fixtures.Sdl2CsSource, []);
        Expect(sdl2Cs.Functions.Any(f => f.ManagedName == "SDL_Init" && f.NativeLibrary == "SDL2"), "extracts SDL2-CS DllImport compatibility functions", failures);

        var entryPointEvidence = CSharpEvidenceExtractor.Extract("entrypoint.cs", Fixtures.EntryPointImportSource, []);
        Expect(entryPointEvidence.Functions.Any(f => f.ManagedName == "INTERNAL_SDL_Init" && f.NativeEntryPoint == "SDL_Init" && f.NativeLibrary == "SDL2" && f.ImportKind == "DllImport"), "extracts native entry point names from DllImport EntryPoint", failures);

        var checks = RawAbiChecks.Run(FamilyConfigs.Sdl2Core, evidence, RequiredSurface.Empty);
        Expect(checks.Any(c => c.CheckId == "raw-abi-public-class"), "flags public raw ABI class", failures);
        Expect(checks.Any(c => c.CheckId == "raw-abi-public-import"), "flags public raw import methods", failures);
        Expect(checks.Any(c => c.CheckId == "deferred-layout-sdl-rwops"), "flags SDL_RWops layout emission", failures);

        var internalRawEvidence = CSharpEvidenceExtractor.Extract("internal-raw-fixture.g.cs", Fixtures.InternalRawAbiVisibilitySource, ["NET5_0_OR_GREATER"]);
        var internalRawChecks = RawAbiChecks.Run(FamilyConfigs.Sdl2Core, internalRawEvidence, RequiredSurface.Empty);
        Expect(!internalRawChecks.Any(c => c.CheckId == "raw-abi-public-class"), "does not flag internal raw ABI class as public", failures);
        Expect(!internalRawChecks.Any(c => c.CheckId == "raw-abi-public-import"), "does not flag public raw imports when the raw ABI class is internal", failures);

        var nestedInternalRawEvidence = CSharpEvidenceExtractor.Extract("nested-internal-raw-fixture.g.cs", Fixtures.NestedInternalRawAbiVisibilitySource, ["NET5_0_OR_GREATER"]);
        var nestedInternalRawChecks = RawAbiChecks.Run(FamilyConfigs.Sdl2Core, nestedInternalRawEvidence, RequiredSurface.Empty);
        Expect(!nestedInternalRawChecks.Any(c => c.CheckId == "raw-abi-public-class"), "does not flag public raw ABI class nested in internal containing type", failures);
        Expect(!nestedInternalRawChecks.Any(c => c.CheckId == "raw-abi-public-import"), "does not flag public raw imports nested in internal containing type", failures);

        var requiredChecks = RawAbiChecks.Run(
            FamilyConfigs.Sdl2Core,
            evidence,
            new RequiredSurface(
                SourceStatus.Present,
                new HashSet<string>(["SDL_MissingFunction"], StringComparer.Ordinal),
                new HashSet<string>(["SDL_MISSING_CONSTANT"], StringComparer.Ordinal)));
        Expect(requiredChecks.Any(c => c.CheckId == "required-function-missing"), "flags missing required functions", failures);
        Expect(requiredChecks.Any(c => c.CheckId == "required-constant-missing"), "flags missing required constants", failures);

        var edgeEvidence = CSharpEvidenceExtractor.Extract("edge-fixture.g.cs", Fixtures.RawAbiEdgeCaseSource, ["NET5_0_OR_GREATER"]);
        var edgeChecks = RawAbiChecks.Run(FamilyConfigs.Sdl2Core, edgeEvidence, RequiredSurface.Empty);
        Expect(edgeChecks.Any(c => c.CheckId == "raw-abi-public-import" && c.Symbol == "SDL_UnresolvedImport"), "flags raw imports with unresolved library argument", failures);
        Expect(edgeChecks.Any(c => c.CheckId == "deferred-layout-sdl-syswminfo"), "flags SDL_SysWMinfo layout emission", failures);
        Expect(edgeChecks.Any(c => c.CheckId == "deferred-layout-sdl-syswmmsg"), "flags SDL_SysWMmsg layout emission", failures);
        Expect(edgeChecks.Any(c => c.CheckId == "platform-sensitive-long"), "flags platform-sensitive C long mappings", failures);
        Expect(edgeChecks.Any(c => c.CheckId == "platform-sensitive-wchar"), "flags platform-sensitive wchar_t mappings", failures);

        var duplicateTagEvidence = CSharpEvidenceExtractor.Extract("duplicate-tag-fixture.g.cs", Fixtures.DuplicateTagTypedefSource, ["NET5_0_OR_GREATER"]);
        var duplicateTagChecks = RawAbiChecks.Run(FamilyConfigs.Sdl2Core, duplicateTagEvidence, RequiredSurface.Empty);
        var duplicateTagFindings = duplicateTagChecks.Where(c => c.CheckId == "duplicate-tag-typedef").ToArray();
        Expect(duplicateTagFindings.Length == 1, "flags exactly one duplicate tag/typedef pair (SDL_hid_device_ vs SDL_hid_device)", failures);
        Expect(duplicateTagFindings.Any(c => c.Symbol == "SDL_hid_device_" && c.Message.Contains("SDL_hid_device *", StringComparison.Ordinal)), "names the tag struct symbol and references the typedef canonical name in the message", failures);
        Expect(duplicateTagFindings.All(c => c.Classification == "Compatibility Risk" && c.Severity == "warning"), "classifies duplicate tag/typedef findings as Compatibility Risk warnings", failures);

        var canonicalTagEvidence = CSharpEvidenceExtractor.Extract("canonical-tag-fixture.g.cs", Fixtures.CanonicalTagTypedefSource, ["NET5_0_OR_GREATER"]);
        var canonicalTagChecks = RawAbiChecks.Run(FamilyConfigs.Sdl2Core, canonicalTagEvidence, RequiredSurface.Empty);
        Expect(!canonicalTagChecks.Any(c => c.CheckId == "duplicate-tag-typedef"), "does not flag canonical tag/typedef state (post-remap baseline)", failures);

        var sampledCheckIds = RawAbiCheckSampler.Sample(Fixtures.DuplicateFirstRawAbiChecks, 4).Select(check => check.CheckId).ToArray();
        Expect(sampledCheckIds.Distinct(StringComparer.Ordinal).Count() > 1, "samples varied raw ABI check categories before truncating", failures);

        var imageEvidence = CSharpEvidenceExtractor.Extract("image-drift-fixture.g.cs", Fixtures.ImageNamespaceDriftSource, ["NET5_0_OR_GREATER"]);
        var imageChecks = RawAbiChecks.Run(FamilyConfigs.Sdl2Image, imageEvidence, RequiredSurface.Empty);
        Expect(imageChecks.Any(c => c.CheckId == "family-namespace-drift"), "flags SDL2_image namespace drift", failures);

        ExpectSatelliteFamilyConfigs(failures);

        var sourceEvidence = CSharpEvidenceLoader.Load("fixture.g.cs", Fixtures.MixedGeneratedSource);
        Expect(sourceEvidence.Status == SourceStatus.Present && sourceEvidence.Evidence.Functions.Count == 5, "loads present C# evidence text", failures);

        var parsed = OracleOptions.Parse(["--family", "sdl2-core", "--family", "sdl2-image", "--write-report"]);
        Expect(parsed.Families.SequenceEqual(["sdl2-core", "sdl2-image"]), "parses repeated --family options", failures);
        Expect(parsed.WriteReport, "parses --write-report", failures);

        Expect(OracleOptions.Parse(["--family"]).ParseError == "Missing value for --family.", "rejects missing --family value", failures);
        Expect(OracleOptions.Parse(["--family", "--write-report"]).ParseError == "Missing value for --family.", "rejects option token after --family", failures);
        Expect(OracleOptions.Parse(["--frobnicate"]).ParseError == "Unknown option: --frobnicate.", "rejects unknown options", failures);

        var repoRoot = OracleOptions.Parse([], Directory.GetCurrentDirectory()).RepoRoot;
        var nestedDirectory = Path.Combine(repoRoot, "spikes", "binding-generators", "clangsharp");
        var nestedParsed = OracleOptions.Parse([], nestedDirectory);
        Expect(Path.GetFullPath(nestedParsed.RepoRoot) == Path.GetFullPath(repoRoot), "detects repo root from nested clangsharp directory", failures);

        var rendererSelfTestReport = CreateRendererSelfTestReport();
        var rendererMarkdown = MarkdownReportRenderer.Render(rendererSelfTestReport);
        Expect(rendererMarkdown.Contains("# ClangSharp Oracle Evidence", StringComparison.Ordinal), "renders report title", failures);
        Expect(rendererMarkdown.Contains("## Inputs", StringComparison.Ordinal), "renders inputs section", failures);
        Expect(rendererMarkdown.Contains("## Family: sdl2-core", StringComparison.Ordinal), "renders sdl2-core family section", failures);
        Expect(rendererMarkdown.Contains("## Family: sdl2-image", StringComparison.Ordinal), "renders sdl2-image family section", failures);
        Expect(rendererMarkdown.Contains("### Raw ABI Constitution Checks", StringComparison.Ordinal), "renders raw ABI constitution checks section", failures);
        Expect(rendererMarkdown.Contains("Hard Bug", StringComparison.Ordinal), "renders raw ABI classifications", failures);
        Expect(rendererMarkdown.Contains("Evidence Missing", StringComparison.Ordinal), "renders evidence gap classifications", failures);
        Expect(!rendererMarkdown.Contains("ManagedHelper", StringComparison.Ordinal), "omits non-import helper methods from function matrix", failures);
        Expect(rendererMarkdown.Contains("`SDL_Init`", StringComparison.Ordinal), "renders native entry point names in function matrix", failures);
        Expect(!rendererMarkdown.Contains("INTERNAL_SDL_Init", StringComparison.Ordinal), "omits managed INTERNAL helper names from function matrix", failures);
        Expect(!rendererMarkdown.Contains(rendererSelfTestReport.RepoRoot, StringComparison.Ordinal), "omits local absolute repo paths from markdown", failures);
        Expect(rendererMarkdown.Contains("`src/core.g.cs`", StringComparison.Ordinal), "renders repo-relative source paths", failures);
        Expect(MarkdownReportRenderer.FormatReportPath(rendererSelfTestReport.RepoRoot, Path.Combine(rendererSelfTestReport.RepoRoot, "src", "core.g.cs")) == "src/core.g.cs", "formats repo-local paths as repo-relative markdown paths", failures);
        Expect(!rendererMarkdown.Contains("Generated UTC", StringComparison.Ordinal), "omits volatile generated timestamp", failures);
        Expect(rendererMarkdown.Contains('\n'), "renders markdown with line breaks", failures);
        Expect(!rendererMarkdown.Contains("\r\n", StringComparison.Ordinal), "renders markdown with LF-only line endings", failures);

        if (failures.Count == 0)
        {
            Console.WriteLine("self-test: PASS");
            return 0;
        }

        Console.Error.WriteLine("self-test: FAIL");
        foreach (var failure in failures)
        {
            Console.Error.WriteLine("- " + failure);
        }

        return 1;
    }

    private static void Expect(bool condition, string message, List<string> failures)
    {
        if (!condition)
        {
            failures.Add(message);
        }
    }

    private static void ExpectSatelliteFamilyConfigs(List<string> failures)
    {
        var expectedFamilies = new[]
        {
            new ExpectedFamilyIdentity("sdl2-ttf", "SDL2.Ttf", "SDL_ttfNative", "external/sdl2-cs/src/SDL2_ttf.cs"),
            new ExpectedFamilyIdentity("sdl2-mixer", "SDL2.Mixer", "SDL_mixerNative", "external/sdl2-cs/src/SDL2_mixer.cs"),
            new ExpectedFamilyIdentity("sdl2-gfx", "SDL2.Gfx", "SDL2_gfxNative", "external/sdl2-cs/src/SDL2_gfx.cs"),
        };

        foreach (var family in expectedFamilies)
        {
            ExpectKnownFamily(family.FamilyId, failures);
            ExpectFamilyConfig(family, failures);
        }
    }

    private static void ExpectKnownFamily(string familyId, List<string> failures)
    {
        var dispatchOptions = OracleOptions.Parse(["--family", familyId]);
        if (dispatchOptions.ParseError is not null)
        {
            failures.Add($"oracle --family {familyId} unexpectedly produced a parse error: {dispatchOptions.ParseError}");
            return;
        }

        var knownFamiliesField = typeof(OracleRunner).GetField(
            "KnownFamilies",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        if (knownFamiliesField?.GetValue(null) is not FamilyConfig[] knownFamilies)
        {
            failures.Add("OracleRunner.KnownFamilies reflection probe failed");
            return;
        }

        if (!knownFamilies.Any(config => config.FamilyId == familyId))
        {
            failures.Add($"OracleRunner.KnownFamilies missing {familyId} entry");
        }
    }

    private static void ExpectFamilyConfig(ExpectedFamilyIdentity family, List<string> failures)
    {
        var config = typeof(FamilyConfigs)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Select(field => field.GetValue(null) as FamilyConfig)
            .FirstOrDefault(config => config?.FamilyId == family.FamilyId);

        if (config is null)
        {
            failures.Add($"FamilyConfigs missing static record for {family.FamilyId}");
            return;
        }

        if (config.ExpectedNamespace != family.Namespace)
        {
            failures.Add($"FamilyConfigs.{family.FamilyId} namespace: expected {family.Namespace}, got {config.ExpectedNamespace}");
        }

        if (config.ExpectedRawClassName != family.RawClass)
        {
            failures.Add($"FamilyConfigs.{family.FamilyId} raw class: expected {family.RawClass}, got {config.ExpectedRawClassName}");
        }

        if (config.UsesSdl2Dynapi)
        {
            failures.Add($"FamilyConfigs.{family.FamilyId} should NOT consume sdl2 dynapi (satellite)");
        }

        if (config.Sdl2CsRelativePath != family.Sdl2CsRelativePath)
        {
            failures.Add($"FamilyConfigs.{family.FamilyId} SDL2-CS path: expected {family.Sdl2CsRelativePath}, got {config.Sdl2CsRelativePath ?? "null"}");
        }
    }

    private sealed record ExpectedFamilyIdentity(string FamilyId, string Namespace, string RawClass, string Sdl2CsRelativePath);

    private static OracleReport CreateRendererSelfTestReport()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "oracle-repo"));
        var corePath = Path.Combine(repoRoot, "src", "core.g.cs");
        var imagePath = Path.Combine(repoRoot, "src", "image.g.cs");
        var coreEvidence = CSharpEvidenceExtractor.Extract(corePath, Fixtures.MixedGeneratedSource, ["NET5_0_OR_GREATER"]);
        var imageEvidence = CSharpEvidenceExtractor.Extract(imagePath, Fixtures.ImageNamespaceDriftSource, ["NET5_0_OR_GREATER"]);
        var entryPointEvidence = CSharpEvidenceExtractor.Extract(Path.Combine(repoRoot, "src", "sdl2-cs.cs"), Fixtures.EntryPointImportSource, []);
        return new OracleReport(
            repoRoot,
            [
                new FamilyReport(
                    FamilyConfigs.Sdl2Core,
                    [
                        new SourceReport("clangsharp-compat", "ClangSharp Compat", corePath, SourceStatus.Present, coreEvidence.Functions.Count, coreEvidence.Constants.Count, coreEvidence.Types.Count),
                        new SourceReport("cake-preview", "Cake Preview", "missing", SourceStatus.Missing, 0, 0, 0)
                    ],
                    new SourceEvidence(SourceStatus.Present, coreEvidence),
                    new SourceEvidence(SourceStatus.Present, entryPointEvidence),
                    new SourceEvidence(SourceStatus.Missing, new CSharpEvidence([], [], [])),
                    new SourceEvidence(SourceStatus.NotApplicable, new CSharpEvidence([], [], [])),
                    new RequiredSurface(SourceStatus.Present, new HashSet<string>(["SDL_MissingFunction"], StringComparer.Ordinal), new HashSet<string>(["SDL_MISSING_CONSTANT"], StringComparer.Ordinal)),
                    new DynapiEvidence(SourceStatus.Present, new HashSet<string>(["SDL_Init"], StringComparer.Ordinal)),
                    [
                        new RawAbiCheck("raw-abi-public-import", "error", "Hard Bug", "SDL_Init", "Raw native import method is public.", corePath),
                        new RawAbiCheck("required-function-missing", "error", "Evidence Missing", "SDL_MissingFunction", "Required function is missing.", "manifest")
                    ]),
                new FamilyReport(
                    FamilyConfigs.Sdl2Image,
                    [new SourceReport("clangsharp-compat", "ClangSharp Compat", imagePath, SourceStatus.Present, imageEvidence.Functions.Count, imageEvidence.Constants.Count, imageEvidence.Types.Count)],
                    new SourceEvidence(SourceStatus.Present, imageEvidence),
                    new SourceEvidence(SourceStatus.NotApplicable, new CSharpEvidence([], [], [])),
                    new SourceEvidence(SourceStatus.NotApplicable, new CSharpEvidence([], [], [])),
                    new SourceEvidence(SourceStatus.NotApplicable, new CSharpEvidence([], [], [])),
                    RequiredSurface.Empty,
                    new DynapiEvidence(SourceStatus.NotApplicable, new HashSet<string>(StringComparer.Ordinal)),
                    [new RawAbiCheck("family-namespace-drift", "error", "Hard Bug", "SDL2", "Generated namespace should be SDL2.Image.", imagePath)])
            ]);
    }
}
