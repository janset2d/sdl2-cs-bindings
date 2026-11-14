#!/usr/bin/env dotnet
#:property TargetFramework=net10.0
#:property PublishAot=false
#:property ManagePackageVersionsCentrally=false
#:package Microsoft.CodeAnalysis.CSharp@4.12.0

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;
using System.Text.Json;

var options = OracleOptions.Parse(args);
if (options.ParseError is not null)
{
    Console.Error.WriteLine(options.ParseError);
    return 2;
}

if (options.ShowHelp)
{
    Console.WriteLine(OracleOptions.HelpText);
    return 0;
}

if (options.SelfTest)
{
    return SelfTests.Run();
}

if (options.Families.Count > 0)
{
    return OracleRunner.Run(options);
}

Console.Error.WriteLine("No families requested. Use --family <id> or --self-test.");
return 2;

internal sealed record OracleOptions(
    bool SelfTest,
    bool ShowHelp,
    string RepoRoot,
    IReadOnlyList<string> Families,
    bool WriteReport,
    bool Stdout,
    string? ParseError)
{
    public const string HelpText = """
usage: dotnet run --file spikes/binding-generators/clangsharp/oracle.cs -- [options]

options:
  --self-test          Run oracle self-tests.
  --family <id>       Process a family. Can be repeated.
  --repo-root <path>  Override repository root. Defaults by probing upward for build/manifest.json.
  --write-report      Write Markdown evidence report under spikes/binding-generators/output/reports/.
  --stdout            Print source status blocks when --write-report is specified.
  --help              Show this help.
""";

    public static OracleOptions Parse(string[] args)
        => Parse(args, Directory.GetCurrentDirectory());

    public static OracleOptions Parse(string[] args, string startDirectory)
    {
        var selfTest = false;
        var help = false;
        var repoRoot = FindRepoRoot(startDirectory);
        var families = new List<string>();
        var writeReport = false;
        var stdout = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.Equals("--self-test", StringComparison.OrdinalIgnoreCase))
            {
                selfTest = true;
                continue;
            }

            if (arg.Equals("--help", StringComparison.OrdinalIgnoreCase))
            {
                help = true;
                continue;
            }

            if (arg.Equals("--family", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    return CreateParseError("Missing value for --family.");
                }

                families.Add(args[++i]);
                continue;
            }

            if (arg.Equals("--repo-root", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    return CreateParseError("Missing value for --repo-root.");
                }

                repoRoot = args[++i];
                continue;
            }

            if (arg.Equals("--write-report", StringComparison.OrdinalIgnoreCase))
            {
                writeReport = true;
                continue;
            }

            if (arg.Equals("--stdout", StringComparison.OrdinalIgnoreCase))
            {
                stdout = true;
                continue;
            }

            return arg.StartsWith("--", StringComparison.Ordinal)
                ? CreateParseError($"Unknown option: {arg}.")
                : CreateParseError($"Unknown argument: {arg}.");
        }

        return new OracleOptions(selfTest, help, repoRoot, families, writeReport, stdout || !writeReport, null);
    }

    private static OracleOptions CreateParseError(string error)
        => new(false, false, FindRepoRoot(Directory.GetCurrentDirectory()), [], false, false, error);

    private static string FindRepoRoot(string startDirectory)
    {
        var directory = new DirectoryInfo(Path.GetFullPath(startDirectory));
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "build", "manifest.json")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return Path.GetFullPath(startDirectory);
    }
}

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

internal static class FamilyConfigs
{
    public static readonly FamilyConfig Sdl2Core = new(
        "sdl2-core",
        "SDL2 Core",
        "SDL2",
        "SDLNative",
        "artifacts/generated-bindings-preview/sdl2-core",
        "spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Compat",
        "spikes/binding-generators/clangsharp/src/Janset.SDL2.Core/Generated/Modern",
        "external/sdl2-cs/src/SDL2.cs",
        true);

    public static readonly FamilyConfig Sdl2Image = new(
        "sdl2-image",
        "SDL2 Image",
        "SDL2.Image",
        "SDL_imageNative",
        null,
        "spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Generated/Compat",
        "spikes/binding-generators/clangsharp/src/Janset.SDL2.Image/Generated/Modern",
        "external/sdl2-cs/src/SDL2_image.cs",
        false);

    public static readonly FamilyConfig Sdl2Ttf = new(
        "sdl2-ttf",
        "SDL2 TTF",
        "SDL2.Ttf",
        "SDL_ttfNative",
        null,
        "spikes/binding-generators/clangsharp/src/Janset.SDL2.Ttf/Generated/Compat",
        "spikes/binding-generators/clangsharp/src/Janset.SDL2.Ttf/Generated/Modern",
        "external/sdl2-cs/src/SDL2_ttf.cs",
        false);

    public static readonly FamilyConfig Sdl2Mixer = new(
        "sdl2-mixer",
        "SDL2 Mixer",
        "SDL2.Mixer",
        "SDL_mixerNative",
        null,
        "spikes/binding-generators/clangsharp/src/Janset.SDL2.Mixer/Generated/Compat",
        "spikes/binding-generators/clangsharp/src/Janset.SDL2.Mixer/Generated/Modern",
        "external/sdl2-cs/src/SDL2_mixer.cs",
        false);

    public static readonly FamilyConfig Sdl2Gfx = new(
        "sdl2-gfx",
        "SDL2 GFX",
        "SDL2.Gfx",
        "SDL2_gfxNative",
        null,
        "spikes/binding-generators/clangsharp/src/Janset.SDL2.Gfx/Generated/Compat",
        "spikes/binding-generators/clangsharp/src/Janset.SDL2.Gfx/Generated/Modern",
        "external/sdl2-cs/src/SDL2_gfx.cs",
        false);
}

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

internal static class Fixtures
{
    public const string DynapiExports = "++'_SDL_Init'.'SDL2.dll'.'SDL_Init'";

    public const string ManifestRequiredSurface = """
{
  "library_manifests": [
    {
      "id": "sdl2",
      "role": "core",
      "binding_generation": {
        "required_functions": {
          "SDL.h": ["SDL_Init"]
        },
        "required_constants": {
          "SDL.h": ["SDL_INIT_VIDEO"]
        }
      }
    }
  ]
}
""";

    public const string Sdl2CsSource = """
using System.Runtime.InteropServices;

namespace SDL2
{
    public static class SDL
    {
        private const string nativeLibName = "SDL2";

        [DllImport(nativeLibName)]
        public static extern int SDL_Init(uint flags);
    }
}
""";

    public const string EntryPointImportSource = """
using System.Runtime.InteropServices;

namespace SDL2
{
    public static class SDL
    {
        private const string nativeLibName = "SDL2";

        [DllImport(nativeLibName, EntryPoint = "SDL_Init")]
        private static extern int INTERNAL_SDL_Init(uint flags);
    }
}
""";

    public const string MixedGeneratedSource = """
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SDL2
{
    public enum SDL_bool
    {
        SDL_FALSE = 0,
        SDL_TRUE = 1,
    }

    public unsafe partial struct SDL_RWops
    {
        public delegate* unmanaged[Cdecl]<SDL_RWops*, long> size;
    }

    public static unsafe partial class SDLNative
    {
        [NativeTypeName("#define SDL_INIT_TIMER 0x00000001u")]
        public const uint SDL_INIT_TIMER = 0x00000001u;

        [NativeTypeName("#define SDL_INIT_AUDIO 0x00000010u")]
        public const uint SDL_INIT_AUDIO = 0x00000010u;

        public const uint SDL_INIT_VIDEO = 0x00000020u;

        [NativeTypeName("#define SDL_INIT_EVERYTHING SDL_INIT_TIMER | SDL_INIT_AUDIO | SDL_INIT_VIDEO")]
        public const uint SDL_INIT_EVERYTHING = SDL_INIT_TIMER | SDL_INIT_AUDIO | SDL_INIT_VIDEO;

        public static ReadOnlySpan<byte> SDL_HINT_RENDER_DRIVER => "SDL_RENDER_DRIVER"u8;

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
        public static extern int SDL_Init(uint flags);

        public static int ManagedHelper() => 42;
    }
}

namespace SDL2.Image
{
    public static unsafe partial class SDL_imageNative
    {
        [LibraryImport("SDL2_image", EntryPoint = "IMG_Init")]
        [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        public static partial int IMG_Init(int flags);
    }
}

namespace SDL2
{
    namespace Image
    {
        public static unsafe partial class NestedNamespaceNative
        {
            private const string LibName = "SDL2";

            [DllImport(LibName)]
            public static extern void SDL_Quit();
        }

        public static unsafe partial class NestedImageNative
        {
            private const string LibName = "SDL2_image";

            [DllImport(LibName)]
            public static extern void IMG_Quit();
        }
    }
}
""";

    public const string RawAbiEdgeCaseSource = """
using System.Runtime.InteropServices;

namespace SDL2
{
    public unsafe partial struct SDL_SysWMinfo
    {
        public int version;
    }

    public unsafe partial struct SDL_SysWMmsg
    {
        public int version;
    }

    public static unsafe partial class SDLNative
    {
        [DllImport(LibName)]
        public static extern void SDL_UnresolvedImport();

        [DllImport("SDL2")]
        [return: NativeTypeName("long")]
        public static extern int SDL_LongReturn([NativeTypeName("unsigned long")] uint value);

        [DllImport("SDL2")]
        public static extern void SDL_WcharParameter([NativeTypeName("const wchar_t *")] ushort* value);
    }
}
""";

    public const string InternalRawAbiVisibilitySource = """
using System.Runtime.InteropServices;

namespace SDL2
{
    internal static unsafe partial class SDLNative
    {
        [DllImport("SDL2")]
        public static extern int SDL_Init(uint flags);
    }

    public static class SDL
    {
        public static int Init(uint flags) => SDLNative.SDL_Init(flags);
    }
}
""";

    public const string NestedInternalRawAbiVisibilitySource = """
using System.Runtime.InteropServices;

namespace SDL2
{
    internal static class Outer
    {
        public static unsafe partial class SDLNative
        {
            [DllImport("SDL2")]
            public static extern int SDL_Init(uint flags);
        }
    }
}
""";

    public static readonly IReadOnlyList<RawAbiCheck> DuplicateFirstRawAbiChecks =
    [
        new("raw-abi-public-class", "error", "Hard Bug", "A", "duplicate", "a.cs"),
        new("raw-abi-public-class", "error", "Hard Bug", "B", "duplicate", "b.cs"),
        new("raw-abi-public-class", "error", "Hard Bug", "C", "duplicate", "c.cs"),
        new("raw-abi-public-class", "error", "Hard Bug", "D", "duplicate", "d.cs"),
        new("raw-abi-public-import", "error", "Hard Bug", "E", "varied", "e.cs"),
        new("platform-sensitive-long", "warning", "Compatibility Risk", "F", "varied", "f.cs")
    ];

    public const string ImageNamespaceDriftSource = """
using System.Runtime.InteropServices;

namespace SDL2
{
    public static partial class SDL_imageNative
    {
        [DllImport("SDL2_image")]
        public static extern int IMG_Init(int flags);
    }
}
""";

    // Mirrors the pre-remap ClangSharp output for SDL_hidapi.h: the C tag `SDL_hid_device_`
    // is emitted as an empty partial struct, and functions reference it via the typedef
    // `SDL_hid_device` through [NativeTypeName] annotations. The adjacent `SDL_hid_device_info`
    // declaration exercises the false-positive guard (legitimately different public type).
    public const string DuplicateTagTypedefSource = """
using System;
using System.Runtime.InteropServices;

namespace SDL2
{
    public partial struct SDL_hid_device_
    {
    }

    public unsafe partial struct SDL_hid_device_info
    {
        public int interface_number;
    }

    public static unsafe partial class SDLNative
    {
        [DllImport("SDL2")]
        [return: NativeTypeName("SDL_hid_device *")]
        public static extern SDL_hid_device_* SDL_hid_open([NativeTypeName("unsigned short")] ushort vendor_id);

        [DllImport("SDL2")]
        public static extern void SDL_hid_close([NativeTypeName("SDL_hid_device *")] SDL_hid_device_* dev);

        [DllImport("SDL2")]
        public static extern SDL_hid_device_info* SDL_hid_enumerate([NativeTypeName("unsigned short")] ushort vendor_id);
    }
}
""";

    // Mirrors the post-remap canonical state: empty struct named after the public typedef,
    // every reference uses the same name, no [NativeTypeName] mismatch. Includes the
    // adjacent `SDL_hid_device_info` type to confirm the guard suppresses unrelated names.
    public const string CanonicalTagTypedefSource = """
using System;
using System.Runtime.InteropServices;

namespace SDL2
{
    public partial struct SDL_hid_device
    {
    }

    public unsafe partial struct SDL_hid_device_info
    {
        public int interface_number;
    }

    public static unsafe partial class SDLNative
    {
        [DllImport("SDL2")]
        public static extern SDL_hid_device* SDL_hid_open([NativeTypeName("unsigned short")] ushort vendor_id);

        [DllImport("SDL2")]
        public static extern void SDL_hid_close(SDL_hid_device* dev);

        [DllImport("SDL2")]
        public static extern SDL_hid_device_info* SDL_hid_enumerate([NativeTypeName("unsigned short")] ushort vendor_id);
    }
}
""";
}

internal sealed record CSharpEvidence(
    IReadOnlyList<FunctionEvidence> Functions,
    IReadOnlyList<ConstantEvidence> Constants,
    IReadOnlyList<TypeEvidence> Types);

internal sealed record FunctionEvidence(
    string ManagedName,
    string NativeEntryPoint,
    string NativeLibrary,
    string ImportKind,
    string Accessibility,
    string ContainingType,
    string ContainingTypePath,
    IReadOnlyList<string> ContainingTypeAccessibilities,
    string NamespaceName,
    string SourcePath,
    string ReturnType,
    string ReturnNativeTypeName,
    IReadOnlyList<ParameterEvidence> Parameters);

internal sealed record ParameterEvidence(string Name, string Type, string NativeTypeName);
internal sealed record ConstantEvidence(string Name, string Kind, string ContainingType, string NamespaceName, string SourcePath);
internal sealed record TypeEvidence(string Name, string Kind, string Accessibility, string ContainingTypePath, IReadOnlyList<string> ContainingTypeAccessibilities, string NamespaceName, string SourcePath, bool HasFields, bool HasNestedFields);
internal sealed record RequiredSurface(SourceStatus Status, IReadOnlySet<string> RequiredFunctions, IReadOnlySet<string> RequiredConstants)
{
    public static RequiredSurface Empty { get; } = new(SourceStatus.NotApplicable, EmptySet(), EmptySet());

    private static IReadOnlySet<string> EmptySet()
        => new HashSet<string>(StringComparer.Ordinal);
}
internal sealed record SourceEvidence(SourceStatus Status, CSharpEvidence Evidence);
internal sealed record DynapiEvidence(SourceStatus Status, IReadOnlySet<string> Exports);
internal sealed record RawAbiCheck(string CheckId, string Severity, string Classification, string Symbol, string Message, string SourcePath);
internal sealed record OracleReport(string RepoRoot, IReadOnlyList<FamilyReport> Families);
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

internal sealed record SourceReport(
    string Id,
    string Name,
    string Path,
    SourceStatus Status,
    int FunctionCount,
    int ConstantCount,
    int TypeCount)
{
    public static SourceReport FromCSharp(string id, string name, string path, SourceEvidence source)
        => new(id, name, path, source.Status, source.Evidence.Functions.Count, source.Evidence.Constants.Count, source.Evidence.Types.Count);

    public static SourceReport FromRequiredSurface(string id, string name, string path, RequiredSurface surface)
        => new(id, name, path, surface.Status, surface.RequiredFunctions.Count, surface.RequiredConstants.Count, 0);

    public static SourceReport FromDynapi(string id, string name, string path, DynapiEvidence evidence)
        => new(id, name, path, evidence.Status, evidence.Exports.Count, 0, 0);
}

internal static class MarkdownReportRenderer
{
    private const int SymbolLimit = 50;

    public static string Render(OracleReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# ClangSharp Oracle Evidence");
        builder.AppendLine();
        builder.AppendLine("This spike report is evidence, not a correctness certificate. Raw ABI constitution checks are listed even when the generated code builds cleanly.");
        builder.AppendLine();
        builder.AppendLine("## Inputs");
        builder.AppendLine();
        builder.AppendLine("| Family | Source | Path | Status | Functions | Constants | Types |");
        builder.AppendLine("| --- | --- | --- | --- | ---: | ---: | ---: |");
        foreach (var family in report.Families.OrderBy(family => family.Config.FamilyId, StringComparer.Ordinal))
        {
            foreach (var source in family.Sources.OrderBy(source => source.Id, StringComparer.Ordinal))
            {
                AppendInputSourceRow(builder, report.RepoRoot, family.Config.FamilyId, source);
            }
        }

        foreach (var family in report.Families.OrderBy(family => family.Config.FamilyId, StringComparer.Ordinal))
        {
            AppendFamily(builder, report.RepoRoot, family);
        }

        return builder.ToString().ReplaceLineEndings("\n");
    }

    private static void AppendFamily(StringBuilder builder, string repoRoot, FamilyReport family)
    {
        builder.AppendLine();
        builder.AppendLine($"## Family: {family.Config.FamilyId}");
        builder.AppendLine();
        builder.AppendLine($"- Display name: {family.Config.DisplayName}");
        builder.AppendLine($"- Expected namespace: `{family.Config.ExpectedNamespace}`");
        builder.AppendLine($"- Expected raw class: `{family.Config.ExpectedRawClassName}`");
        builder.AppendLine();
        AppendSourceStatus(builder, repoRoot, family);
        AppendSurfaceCounts(builder, family);
        AppendRawAbiChecks(builder, repoRoot, family.RawAbiChecks);
        AppendFunctionMatrix(builder, family);
        AppendEvidenceGaps(builder, repoRoot, family);
    }

    private static void AppendSourceStatus(StringBuilder builder, string repoRoot, FamilyReport family)
    {
        builder.AppendLine("### Source Status");
        builder.AppendLine();
        builder.AppendLine("| Source | Path | Status | Functions | Constants | Types |");
        builder.AppendLine("| --- | --- | --- | ---: | ---: | ---: |");
        foreach (var source in family.Sources.OrderBy(source => source.Id, StringComparer.Ordinal))
        {
            AppendFamilySourceRow(builder, repoRoot, source);
        }

        builder.AppendLine();
    }

    private static void AppendSurfaceCounts(StringBuilder builder, FamilyReport family)
    {
        var generated = CSharpEvidenceLoader.Combine(family.ClangSharpCompat.Evidence, family.ClangSharpModern.Evidence);
        builder.AppendLine("### Surface Counts");
        builder.AppendLine();
        builder.AppendLine("| Surface | Functions | Constants | Types |");
        builder.AppendLine("| --- | ---: | ---: | ---: |");
        builder.AppendLine($"| Generated ClangSharp | {generated.Functions.Count} | {generated.Constants.Count} | {generated.Types.Count} |");
        builder.AppendLine($"| Cake Preview | {family.CakePreview.Evidence.Functions.Count} | {family.CakePreview.Evidence.Constants.Count} | {family.CakePreview.Evidence.Types.Count} |");
        builder.AppendLine($"| SDL2-CS | {family.Sdl2Cs.Evidence.Functions.Count} | {family.Sdl2Cs.Evidence.Constants.Count} | {family.Sdl2Cs.Evidence.Types.Count} |");
        builder.AppendLine($"| Dynapi | {family.Dynapi.Exports.Count} | 0 | 0 |");
        builder.AppendLine($"| Manifest Required Surface | {family.RequiredSurface.RequiredFunctions.Count} | {family.RequiredSurface.RequiredConstants.Count} | 0 |");
        builder.AppendLine();
    }

    private static void AppendRawAbiChecks(StringBuilder builder, string repoRoot, IReadOnlyList<RawAbiCheck> checks)
    {
        builder.AppendLine("### Raw ABI Constitution Checks");
        builder.AppendLine();
        if (checks.Count == 0)
        {
            builder.AppendLine("No raw ABI constitution checks were produced.");
            builder.AppendLine();
            return;
        }

        foreach (var classificationGroup in checks.GroupBy(check => check.Classification).OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            builder.AppendLine($"#### {classificationGroup.Key}");
            builder.AppendLine();
            foreach (var checkGroup in classificationGroup.GroupBy(check => check.CheckId).OrderBy(group => RawAbiCheckTitle(group.Key), StringComparer.Ordinal))
            {
                var symbols = checkGroup.Select(check => check.Symbol).Where(symbol => symbol.Length > 0).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
                builder.AppendLine($"- {RawAbiCheckTitle(checkGroup.Key)} (`{checkGroup.Key}`): {checkGroup.Count()} finding(s), {symbols.Length} symbol(s).");
                AppendBoundedSymbols(builder, symbols);
                foreach (var example in checkGroup.OrderBy(check => check.Symbol, StringComparer.Ordinal).ThenBy(check => check.SourcePath, StringComparer.Ordinal).Take(3))
                {
                    builder.AppendLine($"  - Example: `{EscapeInline(example.Symbol)}` - {EscapeText(example.Message)} (`{EscapeInline(FormatReportPath(repoRoot, example.SourcePath))}`)");
                }
            }

            builder.AppendLine();
        }
    }

    private static void AppendFunctionMatrix(StringBuilder builder, FamilyReport family)
    {
        var generated = NativeImportNames(CSharpEvidenceLoader.Combine(family.ClangSharpCompat.Evidence, family.ClangSharpModern.Evidence));
        var cake = NativeImportNames(family.CakePreview.Evidence);
        var sdl2Cs = NativeImportNames(family.Sdl2Cs.Evidence);
        var dynapi = family.Dynapi.Exports;
        var symbols = generated.Concat(cake).Concat(sdl2Cs).Concat(dynapi).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

        builder.AppendLine("### Function Matrix");
        builder.AppendLine();
        builder.AppendLine("| Function | Generated | Cake | SDL2-CS | Dynapi |");
        builder.AppendLine("| --- | --- | --- | --- | --- |");
        foreach (var symbol in symbols.Take(SymbolLimit))
        {
            builder.AppendLine($"| `{EscapeInline(symbol)}` | {YesNo(generated.Contains(symbol))} | {YesNoOrNa(family.CakePreview.Status, cake.Contains(symbol))} | {YesNoOrNa(family.Sdl2Cs.Status, sdl2Cs.Contains(symbol))} | {YesNoOrNa(family.Dynapi.Status, dynapi.Contains(symbol))} |");
        }

        if (symbols.Length > SymbolLimit)
        {
            builder.AppendLine($"| ... | {symbols.Length - SymbolLimit} additional sorted function(s) omitted for readability. |  |  |  |");
        }

        builder.AppendLine();
    }

    private static void AppendEvidenceGaps(StringBuilder builder, string repoRoot, FamilyReport family)
    {
        builder.AppendLine("### Evidence Gaps");
        builder.AppendLine();
        var gaps = new List<string>();
        foreach (var source in family.Sources.Where(source => source.Status is SourceStatus.Missing).OrderBy(source => source.Id, StringComparer.Ordinal))
        {
            gaps.Add($"Evidence Missing: {source.Name} at `{EscapeInline(FormatReportPath(repoRoot, source.Path))}`.");
        }

        foreach (var check in family.RawAbiChecks.Where(check => check.CheckId is "required-function-missing" or "required-constant-missing").OrderBy(check => check.CheckId, StringComparer.Ordinal).ThenBy(check => check.Symbol, StringComparer.Ordinal))
        {
            gaps.Add($"Evidence Missing: {RawAbiCheckTitle(check.CheckId)} - `{EscapeInline(check.Symbol)}`.");
        }

        if (gaps.Count == 0)
        {
            builder.AppendLine("No evidence gaps were detected for loaded sources and manifest-required checks.");
            builder.AppendLine();
            return;
        }

        foreach (var gap in gaps)
        {
            builder.AppendLine("- " + gap);
        }

        builder.AppendLine();
    }

    private static void AppendBoundedSymbols(StringBuilder builder, IReadOnlyList<string> symbols)
    {
        if (symbols.Count == 0)
        {
            return;
        }

        builder.AppendLine("  - Symbols:");
        foreach (var symbol in symbols.Take(SymbolLimit))
        {
            builder.AppendLine($"  - `{EscapeInline(symbol)}`");
        }

        if (symbols.Count > SymbolLimit)
        {
            builder.AppendLine($"  - ... {symbols.Count - SymbolLimit} remaining symbol(s).");
        }
    }

    private static void AppendInputSourceRow(StringBuilder builder, string repoRoot, string familyId, SourceReport source)
        => builder.AppendLine($"| {familyId} | {source.Name} | `{EscapeInline(FormatReportPath(repoRoot, source.Path))}` | {FormatStatus(source.Status)} | {source.FunctionCount} | {source.ConstantCount} | {source.TypeCount} |");

    private static void AppendFamilySourceRow(StringBuilder builder, string repoRoot, SourceReport source)
        => builder.AppendLine($"| {source.Name} | `{EscapeInline(FormatReportPath(repoRoot, source.Path))}` | {FormatStatus(source.Status)} | {source.FunctionCount} | {source.ConstantCount} | {source.TypeCount} |");

    private static IReadOnlySet<string> NativeImportNames(CSharpEvidence evidence)
        => evidence.Functions
            .Where(function => function.ImportKind.Length > 0)
            .Select(function => function.NativeEntryPoint)
            .ToHashSet(StringComparer.Ordinal);

    public static string FormatReportPath(string repoRoot, string path)
    {
        if (path.Length == 0 || path == "n/a" || path == "manifest" || !Path.IsPathFullyQualified(path))
        {
            return path.Replace('\\', '/');
        }

        var fullRoot = Path.GetFullPath(repoRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(path);
        if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            return fullPath.Replace('\\', '/');
        }

        return Path.GetRelativePath(fullRoot, fullPath).Replace('\\', '/');
    }

    private static string RawAbiCheckTitle(string checkId)
        => checkId switch
        {
            "raw-abi-public-class" or "raw-abi-public-import" => "Public raw ABI leak",
            "required-function-missing" => "Missing SDL.h required functions",
            "required-constant-missing" => "Missing SDL.h required constants",
            "deferred-layout-sdl-rwops" or "deferred-layout-sdl-syswminfo" or "deferred-layout-sdl-syswmmsg" => "Deferred layout violations",
            "platform-sensitive-long" or "platform-sensitive-wchar" => "Platform-sensitive scalar risks",
            "duplicate-tag-typedef" => "Duplicate tag/typedef pairs",
            "family-namespace-drift" => "Image namespace drift",
            _ => checkId
        };

    private static string FormatStatus(SourceStatus status)
        => status switch
        {
            SourceStatus.Present => "present",
            SourceStatus.Missing => "missing",
            SourceStatus.NotApplicable => "n/a",
            SourceStatus.OutOfScope => "out-of-scope",
            _ => status.ToString()
        };

    private static string YesNo(bool value)
        => value ? "yes" : "no";

    private static string YesNoOrNa(SourceStatus status, bool value)
        => status is SourceStatus.NotApplicable or SourceStatus.OutOfScope ? "n/a" : YesNo(value);

    private static string EscapeInline(string text)
        => text.Replace("`", "'", StringComparison.Ordinal).Replace("|", "\\|", StringComparison.Ordinal);

    private static string EscapeText(string text)
        => text.Replace("|", "\\|", StringComparison.Ordinal);
}

internal static class RawAbiCheckSampler
{
    public static IReadOnlyList<RawAbiCheck> Sample(IReadOnlyList<RawAbiCheck> checks, int count)
    {
        if (count <= 0 || checks.Count == 0)
        {
            return [];
        }

        var sample = new List<RawAbiCheck>(count);
        var seenCheckIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var check in checks)
        {
            if (seenCheckIds.Add(check.CheckId))
            {
                sample.Add(check);
                if (sample.Count == count)
                {
                    return sample;
                }
            }
        }

        foreach (var check in checks)
        {
            if (sample.Contains(check))
            {
                continue;
            }

            sample.Add(check);
            if (sample.Count == count)
            {
                break;
            }
        }

        return sample;
    }
}

internal static class RawAbiChecks
{
    public static IReadOnlyList<RawAbiCheck> Run(FamilyConfig config, CSharpEvidence evidence, RequiredSurface required)
    {
        var checks = new List<RawAbiCheck>();
        var publicRawContainers = evidence.Types
            .Where(type => IsExpectedRawClass(config, type) && IsEffectivelyPublic(type.Accessibility, type.ContainingTypeAccessibilities))
            .Select(type => (type.NamespaceName, type.ContainingTypePath))
            .ToHashSet();

        foreach (var type in evidence.Types.Where(type => IsExpectedRawClass(config, type) && IsEffectivelyPublic(type.Accessibility, type.ContainingTypeAccessibilities)))
        {
            checks.Add(HardBug("raw-abi-public-class", type.Name, "Raw ABI class is public; generated raw extern containers must be internal.", type.SourcePath));
        }

        foreach (var function in evidence.Functions.Where(function => IsRawImport(config, function, publicRawContainers) && IsEffectivelyPublic(function.Accessibility, function.ContainingTypeAccessibilities)))
        {
            checks.Add(HardBug("raw-abi-public-import", function.ManagedName, "Raw native import method is public; generated raw externs must be internal.", function.SourcePath));
        }

        if (config.FamilyId.Equals("sdl2-core", StringComparison.OrdinalIgnoreCase) && required.Status == SourceStatus.Present)
        {
            var generatedFunctions = evidence.Functions
                .Where(function => IsNativeImport(function))
                .Select(function => function.ManagedName)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var requiredFunction in required.RequiredFunctions.Where(requiredFunction => !generatedFunctions.Contains(requiredFunction)).Order(StringComparer.Ordinal))
            {
                checks.Add(HardBug("required-function-missing", requiredFunction, "Manifest-required SDL2 function is absent from ClangSharp generated evidence.", "manifest"));
            }

            var generatedConstants = evidence.Constants.Select(constant => constant.Name).ToHashSet(StringComparer.Ordinal);
            foreach (var requiredConstant in required.RequiredConstants.Where(requiredConstant => !generatedConstants.Contains(requiredConstant)).Order(StringComparer.Ordinal))
            {
                checks.Add(HardBug("required-constant-missing", requiredConstant, "Manifest-required SDL2 constant is absent from ClangSharp generated evidence.", "manifest"));
            }
        }

        AddDeferredLayoutChecks(checks, evidence, "SDL_RWops", "deferred-layout-sdl-rwops", includeNestedFields: true);
        AddDeferredLayoutChecks(checks, evidence, "SDL_SysWMinfo", "deferred-layout-sdl-syswminfo", includeNestedFields: false);
        AddDeferredLayoutChecks(checks, evidence, "SDL_SysWMmsg", "deferred-layout-sdl-syswmmsg", includeNestedFields: false);

        AddDuplicateTagTypedefChecks(checks, evidence);

        foreach (var function in evidence.Functions.Where(IsNativeImport))
        {
            if (!IsClongDualDispatchHelper(function)
                && (IsPlatformSensitiveLong(function.ReturnNativeTypeName, function.ReturnType)
                || function.Parameters.Any(parameter => IsPlatformSensitiveLong(parameter.NativeTypeName, parameter.Type)))
               )
            {
                checks.Add(new RawAbiCheck(
                    "platform-sensitive-long",
                    "warning",
                    "Compatibility Risk",
                    function.ManagedName,
                    "C long/unsigned long is mapped to int/uint, which is width-sensitive across supported platforms.",
                    function.SourcePath));
            }

            if (IsPlatformSensitiveWchar(function.ReturnNativeTypeName, function.ReturnType)
                || function.Parameters.Any(parameter => IsPlatformSensitiveWchar(parameter.NativeTypeName, parameter.Type)))
            {
                checks.Add(new RawAbiCheck(
                    "platform-sensitive-wchar",
                    "warning",
                    "Compatibility Risk",
                    function.ManagedName,
                    "wchar_t is mapped to ushort*, which is platform-sensitive outside Windows.",
                    function.SourcePath));
            }
        }

        foreach (var drift in evidence.Types.Select(type => (type.NamespaceName, type.SourcePath))
            .Concat(evidence.Functions.Select(function => (function.NamespaceName, function.SourcePath)))
            .Concat(evidence.Constants.Select(constant => (constant.NamespaceName, constant.SourcePath)))
            .Where(item => item.NamespaceName.Length > 0 && item.NamespaceName != config.ExpectedNamespace)
            .Distinct()
            .OrderBy(item => item.NamespaceName, StringComparer.Ordinal))
        {
            checks.Add(HardBug("family-namespace-drift", drift.NamespaceName, $"Generated namespace should be {config.ExpectedNamespace} for {config.FamilyId}.", drift.SourcePath));
        }

        return checks;
    }

    private static void AddDeferredLayoutChecks(List<RawAbiCheck> checks, CSharpEvidence evidence, string typeName, string checkId, bool includeNestedFields)
    {
        foreach (var type in evidence.Types.Where(type => type.Kind == "Struct" && type.Name == typeName && (type.HasFields || (includeNestedFields && type.HasNestedFields))))
        {
            checks.Add(HardBug(checkId, type.Name, $"{typeName} layout is deferred but ClangSharp emitted fields.", type.SourcePath));
        }
    }

    private static void AddDuplicateTagTypedefChecks(List<RawAbiCheck> checks, CSharpEvidence evidence)
    {
        var declaredStructNames = evidence.Types
            .Where(type => type.Kind == "Struct")
            .Select(type => type.Name)
            .ToHashSet(StringComparer.Ordinal);

        var emptyStructs = evidence.Types
            .Where(type => type.Kind == "Struct" && !type.HasFields && !type.HasNestedFields)
            .ToArray();

        foreach (var structEvidence in emptyStructs)
        {
            var reported = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (annotationCanonical, sourcePath) in EnumerateMismatchedReferences(structEvidence.Name, evidence.Functions))
            {
                if (declaredStructNames.Contains(annotationCanonical) || !reported.Add(annotationCanonical))
                {
                    continue;
                }

                checks.Add(new RawAbiCheck(
                    "duplicate-tag-typedef",
                    "warning",
                    "Compatibility Risk",
                    structEvidence.Name,
                    $"Struct '{structEvidence.Name}' is referenced via [NativeTypeName(\"{annotationCanonical} *\")] - tag/typedef pair detected; canonicalize via per-header RSP --remap (Constitution section \"Opaque Handles\").",
                    sourcePath));
            }
        }
    }

    private static IEnumerable<(string AnnotationCanonical, string SourcePath)> EnumerateMismatchedReferences(string structName, IReadOnlyList<FunctionEvidence> functions)
    {
        foreach (var function in functions)
        {
            if (ReferencesManagedType(function.ReturnType, structName))
            {
                var canonical = TryCanonicalizeNativeTypeName(function.ReturnNativeTypeName);
                if (canonical is not null && !canonical.Equals(structName, StringComparison.Ordinal))
                {
                    yield return (canonical, function.SourcePath);
                }
            }

            foreach (var parameter in function.Parameters)
            {
                if (!ReferencesManagedType(parameter.Type, structName))
                {
                    continue;
                }

                var canonical = TryCanonicalizeNativeTypeName(parameter.NativeTypeName);
                if (canonical is not null && !canonical.Equals(structName, StringComparison.Ordinal))
                {
                    yield return (canonical, function.SourcePath);
                }
            }
        }
    }

    private static bool ReferencesManagedType(string managedType, string structName)
        => StripPointerAndWhitespace(managedType).Equals(structName, StringComparison.Ordinal);

    private static string? TryCanonicalizeNativeTypeName(string nativeTypeName)
    {
        if (nativeTypeName.Length == 0)
        {
            return null;
        }

        var canonical = nativeTypeName
            .Replace("const ", "", StringComparison.Ordinal)
            .Replace("struct ", "", StringComparison.Ordinal);
        canonical = StripPointerAndWhitespace(canonical);
        return canonical.Length == 0 ? null : canonical;
    }

    private static string StripPointerAndWhitespace(string text)
        => text.Replace("*", "", StringComparison.Ordinal).Trim();

    private static bool IsExpectedRawClass(FamilyConfig config, TypeEvidence type)
        => type.Kind == "Class"
            && type.Name == config.ExpectedRawClassName;

    private static bool IsRawImport(FamilyConfig config, FunctionEvidence function, IReadOnlySet<(string NamespaceName, string ContainingTypePath)> publicRawContainers)
        => IsNativeImport(function)
            && function.ContainingType == config.ExpectedRawClassName
            && publicRawContainers.Contains((function.NamespaceName, function.ContainingTypePath));

    private static bool IsEffectivelyPublic(string accessibility, IReadOnlyList<string> containingTypeAccessibilities)
        => accessibility == "public"
            && containingTypeAccessibilities.All(accessibility => accessibility == "public");

    private static bool IsNativeImport(FunctionEvidence function)
        => function.ImportKind.Length > 0;

    private static bool IsClongDualDispatchHelper(FunctionEvidence function)
        => function.Accessibility == "private"
            && (function.ManagedName.EndsWith("_Win32", StringComparison.Ordinal)
                || function.ManagedName.EndsWith("_Unix64", StringComparison.Ordinal));

    private static bool IsPlatformSensitiveLong(string nativeTypeName, string managedType)
        => (nativeTypeName == "long" && managedType == "int")
            || (nativeTypeName == "unsigned long" && managedType == "uint");

    private static bool IsPlatformSensitiveWchar(string nativeTypeName, string managedType)
        => nativeTypeName.Contains("wchar_t", StringComparison.Ordinal) && managedType == "ushort*";

    private static RawAbiCheck HardBug(string checkId, string symbol, string message, string sourcePath)
        => new(checkId, "error", "Hard Bug", symbol, message, sourcePath);
}

internal enum SourceStatus
{
    Present,
    Missing,
    NotApplicable,
    OutOfScope
}

internal static class DynapiParser
{
    public static IReadOnlySet<string> Parse(string text)
    {
        const char quote = (char)39;
        var exports = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var fields = line.Split(quote);
            if (fields.Length >= 6)
            {
                exports.Add(fields[5]);
            }
        }

        return exports;
    }
}

internal static class DynapiEvidenceLoader
{
    public static DynapiEvidence Load(string repoRoot)
    {
        var exports = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in FindDynapiExportPaths(repoRoot))
        {
            foreach (var export in DynapiParser.Parse(File.ReadAllText(path)))
            {
                exports.Add(export);
            }
        }

        return exports.Count == 0
            ? new DynapiEvidence(SourceStatus.Missing, exports)
            : new DynapiEvidence(SourceStatus.Present, exports);
    }

    private static IEnumerable<string> FindDynapiExportPaths(string repoRoot)
    {
        var candidateRoots = new[]
        {
            Path.Combine(repoRoot, "external", "vcpkg", "buildtrees", "sdl2", "src"),
            Path.Combine(repoRoot, "vcpkg_installed", "vcpkg", "blds", "sdl2", "src")
        };

        foreach (var candidateRoot in candidateRoots)
        {
            if (!Directory.Exists(candidateRoot))
            {
                continue;
            }

            foreach (var versionDirectory in Directory.EnumerateDirectories(candidateRoot).Order(StringComparer.Ordinal))
            {
                var exportPath = Path.Combine(versionDirectory, "src", "dynapi", "SDL2.exports");
                if (File.Exists(exportPath))
                {
                    yield return exportPath;
                }
            }
        }
    }
}

internal static class ManifestRequiredSurfaceReader
{
    public static RequiredSurface Read(string repoRoot)
    {
        var manifestPath = Path.Combine(repoRoot, "build", "manifest.json");
        return File.Exists(manifestPath)
            ? ReadJson(File.ReadAllText(manifestPath))
            : new RequiredSurface(SourceStatus.Missing, EmptySet(), EmptySet());
    }

    public static RequiredSurface ReadJson(string text)
    {
        using var document = JsonDocument.Parse(text);
        if (!document.RootElement.TryGetProperty("library_manifests", out var libraryManifests)
            || libraryManifests.ValueKind is not JsonValueKind.Array)
        {
            return new RequiredSurface(SourceStatus.Missing, EmptySet(), EmptySet());
        }

        foreach (var libraryManifest in libraryManifests.EnumerateArray())
        {
            if (!IsSdl2Core(libraryManifest)
                || !libraryManifest.TryGetProperty("binding_generation", out var bindingGeneration))
            {
                continue;
            }

            return new RequiredSurface(
                SourceStatus.Present,
                ReadRequiredNames(bindingGeneration, "required_functions"),
                ReadRequiredNames(bindingGeneration, "required_constants"));
        }

        return new RequiredSurface(SourceStatus.Missing, EmptySet(), EmptySet());
    }

    private static bool IsSdl2Core(JsonElement libraryManifest)
    {
        var nameMatches = TryGetString(libraryManifest, "name", out var name)
            && name.Equals("SDL2", StringComparison.OrdinalIgnoreCase);
        var vcpkgMatches = TryGetString(libraryManifest, "vcpkg_name", out var vcpkgName)
            && vcpkgName.Equals("sdl2", StringComparison.OrdinalIgnoreCase);
        var idMatches = TryGetString(libraryManifest, "id", out var id)
            && id.Equals("sdl2", StringComparison.OrdinalIgnoreCase);
        var coreMatches = (libraryManifest.TryGetProperty("core_lib", out var coreLib) && coreLib.ValueKind is JsonValueKind.True)
            || (TryGetString(libraryManifest, "role", out var role) && role.Equals("core", StringComparison.OrdinalIgnoreCase));

        return (nameMatches || vcpkgMatches || idMatches) && coreMatches;
    }

    private static IReadOnlySet<string> ReadRequiredNames(JsonElement bindingGeneration, string propertyName)
    {
        if (!bindingGeneration.TryGetProperty(propertyName, out var property))
        {
            return EmptySet();
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        if (property.ValueKind is JsonValueKind.Array)
        {
            foreach (var item in property.EnumerateArray())
            {
                if (item.ValueKind is JsonValueKind.Object && TryGetString(item, "name", out var objectName))
                {
                    names.Add(objectName);
                }
                else if (item.ValueKind is JsonValueKind.String && item.GetString() is { } stringName)
                {
                    names.Add(stringName);
                }
            }
        }
        else if (property.ValueKind is JsonValueKind.Object)
        {
            foreach (var header in property.EnumerateObject())
            {
                if (header.Value.ValueKind is not JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var item in header.Value.EnumerateArray())
                {
                    if (item.ValueKind is JsonValueKind.String && item.GetString() is { } name)
                    {
                        names.Add(name);
                    }
                }
            }
        }

        return names;
    }

    private static bool TryGetString(JsonElement element, string propertyName, out string value)
    {
        if (element.TryGetProperty(propertyName, out var property) && property.GetString() is { } text)
        {
            value = text;
            return true;
        }

        value = "";
        return false;
    }

    private static IReadOnlySet<string> EmptySet()
        => new HashSet<string>(StringComparer.Ordinal);
}

internal static class CSharpEvidenceLoader
{
    public static SourceEvidence LoadPath(string repoRoot, string relativePath)
    {
        var path = Path.Combine(repoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(path))
        {
            return Load(path, File.ReadAllText(path));
        }

        if (Directory.Exists(path))
        {
            var evidence = new CSharpEvidence([], [], []);
            foreach (var file in Directory.EnumerateFiles(path, "*.cs", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
            {
                evidence = Combine(evidence, CSharpEvidenceExtractor.Extract(file, File.ReadAllText(file), ["NET5_0_OR_GREATER"]));
            }

            return new SourceEvidence(SourceStatus.Present, evidence);
        }

        return new SourceEvidence(SourceStatus.Missing, new CSharpEvidence([], [], []));
    }

    public static SourceEvidence Load(string path, string source)
        => new(SourceStatus.Present, CSharpEvidenceExtractor.Extract(path, source, ["NET5_0_OR_GREATER"]));

    public static CSharpEvidence Combine(CSharpEvidence left, CSharpEvidence right)
        => new(
            left.Functions.Concat(right.Functions).ToArray(),
            left.Constants.Concat(right.Constants).ToArray(),
            left.Types.Concat(right.Types).ToArray());
}

internal static class CSharpEvidenceExtractor
{
    public static CSharpEvidence Extract(string path, string source, string[] preprocessorSymbols)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(
            source,
            CSharpParseOptions.Default.WithPreprocessorSymbols(preprocessorSymbols),
            path: path);
        var root = syntaxTree.GetCompilationUnitRoot();

        var functions = root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Select(CreateFunctionEvidence)
            .ToArray();

        var fields = root.DescendantNodes()
            .OfType<FieldDeclarationSyntax>()
            .Where(IsConstantLikeField)
            .SelectMany(CreateFieldEvidence);
        var properties = root.DescendantNodes()
            .OfType<PropertyDeclarationSyntax>()
            .Where(property => property.ExpressionBody is not null)
            .Select(CreatePropertyEvidence);
        var constants = fields.Concat(properties).ToArray();

        var types = root.DescendantNodes()
            .Where(node => node is ClassDeclarationSyntax or StructDeclarationSyntax or EnumDeclarationSyntax)
            .Select(CreateTypeEvidence)
            .ToArray();

        return new CSharpEvidence(functions, constants, types);
    }

    private static FunctionEvidence CreateFunctionEvidence(MethodDeclarationSyntax method)
    {
        var import = FindImportAttribute(method);
        return new FunctionEvidence(
            method.Identifier.ValueText,
            import.NativeEntryPoint.Length == 0 ? method.Identifier.ValueText : import.NativeEntryPoint,
            import.NativeLibrary,
            import.Kind,
            GetAccessibility(method.Modifiers),
            GetContainingType(method),
            GetContainingTypePath(method),
            GetContainingTypeAccessibilities(method),
            GetNamespaceName(method),
            method.SyntaxTree.FilePath,
            method.ReturnType.ToString(),
            GetReturnNativeTypeName(method),
            method.ParameterList.Parameters.Select(CreateParameterEvidence).ToArray());
    }

    private static ParameterEvidence CreateParameterEvidence(ParameterSyntax parameter)
        => new(
            parameter.Identifier.ValueText,
            parameter.Type?.ToString() ?? "",
            GetNativeTypeName(parameter.AttributeLists));

    private static ConstantEvidence CreatePropertyEvidence(PropertyDeclarationSyntax property)
        => new(
            property.Identifier.ValueText,
            "Property",
            GetContainingType(property),
            GetNamespaceName(property),
            property.SyntaxTree.FilePath);

    private static IEnumerable<ConstantEvidence> CreateFieldEvidence(FieldDeclarationSyntax field)
    {
        foreach (var variable in field.Declaration.Variables)
        {
            yield return new ConstantEvidence(
                variable.Identifier.ValueText,
                "Field",
                GetContainingType(field),
                GetNamespaceName(field),
                field.SyntaxTree.FilePath);
        }
    }

    private static TypeEvidence CreateTypeEvidence(SyntaxNode type)
    {
        return type switch
        {
            ClassDeclarationSyntax @class => new TypeEvidence(
                @class.Identifier.ValueText,
                "Class",
                GetAccessibility(@class.Modifiers),
                GetContainingTypePath(@class),
                GetContainingTypeAccessibilities(@class),
                GetNamespaceName(@class),
                @class.SyntaxTree.FilePath,
                HasDirectFields(@class),
                HasNestedFields(@class)),
            StructDeclarationSyntax structure => new TypeEvidence(
                structure.Identifier.ValueText,
                "Struct",
                GetAccessibility(structure.Modifiers),
                GetContainingTypePath(structure),
                GetContainingTypeAccessibilities(structure),
                GetNamespaceName(structure),
                structure.SyntaxTree.FilePath,
                HasDirectFields(structure),
                HasNestedFields(structure)),
            EnumDeclarationSyntax enumeration => new TypeEvidence(
                enumeration.Identifier.ValueText,
                "Enum",
                GetAccessibility(enumeration.Modifiers),
                GetContainingTypePath(enumeration),
                GetContainingTypeAccessibilities(enumeration),
                GetNamespaceName(enumeration),
                enumeration.SyntaxTree.FilePath,
                false,
                false),
            _ => throw new InvalidOperationException("Unsupported type syntax node.")
        };
    }

    private static bool HasDirectFields(TypeDeclarationSyntax type)
        => type.Members.OfType<FieldDeclarationSyntax>().Any();

    private static bool HasNestedFields(TypeDeclarationSyntax type)
        => type.Members.OfType<TypeDeclarationSyntax>().Any(nested => HasDirectFields(nested) || HasNestedFields(nested));

    private static bool IsConstantLikeField(FieldDeclarationSyntax field)
    {
        var modifiers = field.Modifiers;
        return modifiers.Any(SyntaxKind.ConstKeyword)
            || (modifiers.Any(SyntaxKind.StaticKeyword) && modifiers.Any(SyntaxKind.ReadOnlyKeyword));
    }

    private static ImportEvidence FindImportAttribute(MethodDeclarationSyntax method)
    {
        foreach (var attribute in method.AttributeLists.SelectMany(list => list.Attributes))
        {
            var attributeName = attribute.Name.ToString();
            if (!IsImportAttribute(attributeName, out var importKind))
            {
                continue;
            }

            return new ImportEvidence(
                importKind,
                GetImportLibraryArgument(attribute, method),
                GetImportEntryPoint(attribute, method.Identifier.ValueText));
        }

        return new ImportEvidence("", "", "");
    }

    private static bool IsImportAttribute(string attributeName, out string importKind)
    {
        if (MatchesAttribute(attributeName, "DllImport"))
        {
            importKind = "DllImport";
            return true;
        }

        if (MatchesAttribute(attributeName, "LibraryImport"))
        {
            importKind = "LibraryImport";
            return true;
        }

        importKind = "";
        return false;
    }

    private static bool MatchesAttribute(string attributeName, string simpleName)
        => attributeName == simpleName
            || attributeName == simpleName + "Attribute"
            || attributeName.EndsWith("." + simpleName, StringComparison.Ordinal)
            || attributeName.EndsWith("." + simpleName + "Attribute", StringComparison.Ordinal);

    private static string GetReturnNativeTypeName(MethodDeclarationSyntax method)
        => GetNativeTypeName(method.AttributeLists.Where(list => list.Target?.Identifier.IsKind(SyntaxKind.ReturnKeyword) == true));

    private static string GetNativeTypeName(IEnumerable<AttributeListSyntax> attributeLists)
    {
        foreach (var attribute in attributeLists.SelectMany(list => list.Attributes))
        {
            if (MatchesAttribute(attribute.Name.ToString(), "NativeTypeName")
                && attribute.ArgumentList?.Arguments.FirstOrDefault()?.Expression is LiteralExpressionSyntax literal
                && literal.IsKind(SyntaxKind.StringLiteralExpression))
            {
                return literal.Token.ValueText;
            }
        }

        return "";
    }

    private static string GetImportLibraryArgument(AttributeSyntax attribute, MethodDeclarationSyntax method)
    {
        var expression = attribute.ArgumentList?.Arguments.FirstOrDefault()?.Expression;
        return expression switch
        {
            LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression) => literal.Token.ValueText,
            IdentifierNameSyntax identifier => ResolveStringConstant(method, identifier.Identifier.ValueText),
            _ => ""
        };
    }

    private static string GetImportEntryPoint(AttributeSyntax attribute, string managedName)
    {
        foreach (var argument in attribute.ArgumentList?.Arguments ?? [])
        {
            if (argument.NameEquals?.Name.Identifier.ValueText == "EntryPoint"
                && argument.Expression is LiteralExpressionSyntax literal
                && literal.IsKind(SyntaxKind.StringLiteralExpression))
            {
                return literal.Token.ValueText;
            }
        }

        return managedName;
    }

    private static string ResolveStringConstant(MethodDeclarationSyntax method, string identifierName)
    {
        foreach (var type in method.Ancestors().OfType<TypeDeclarationSyntax>())
        {
            foreach (var field in type.Members.OfType<FieldDeclarationSyntax>())
            {
                if (!field.Modifiers.Any(SyntaxKind.ConstKeyword) || field.Declaration.Type.ToString() != "string")
                {
                    continue;
                }

                foreach (var variable in field.Declaration.Variables)
                {
                    if (variable.Identifier.ValueText == identifierName
                        && variable.Initializer?.Value is LiteralExpressionSyntax literal
                        && literal.IsKind(SyntaxKind.StringLiteralExpression))
                    {
                        return literal.Token.ValueText;
                    }
                }
            }
        }

        return "";
    }

    private static string GetAccessibility(SyntaxTokenList modifiers)
    {
        if (modifiers.Any(SyntaxKind.PublicKeyword))
        {
            return "public";
        }

        if (modifiers.Any(SyntaxKind.InternalKeyword))
        {
            return "internal";
        }

        if (modifiers.Any(SyntaxKind.ProtectedKeyword) && modifiers.Any(SyntaxKind.PrivateKeyword))
        {
            return "private protected";
        }

        if (modifiers.Any(SyntaxKind.ProtectedKeyword) && modifiers.Any(SyntaxKind.InternalKeyword))
        {
            return "protected internal";
        }

        if (modifiers.Any(SyntaxKind.ProtectedKeyword))
        {
            return "protected";
        }

        if (modifiers.Any(SyntaxKind.PrivateKeyword))
        {
            return "private";
        }

        return "";
    }

    private static string GetContainingType(SyntaxNode node)
        => node.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault()?.Identifier.ValueText ?? "";

    private static string GetContainingTypePath(SyntaxNode node)
    {
        var containingTypes = node.Ancestors().OfType<TypeDeclarationSyntax>().Reverse();
        if (node is TypeDeclarationSyntax type)
        {
            containingTypes = containingTypes.Append(type);
        }

        return string.Join(".", containingTypes.Select(type => type.Identifier.ValueText));
    }

    private static IReadOnlyList<string> GetContainingTypeAccessibilities(SyntaxNode node)
        => node.Ancestors().OfType<TypeDeclarationSyntax>().Reverse().Select(type => GetAccessibility(type.Modifiers)).ToArray();

    private static string GetNamespaceName(SyntaxNode node)
        => string.Join(".", node.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().Reverse().Select(ns => ns.Name.ToString()));

    private readonly record struct ImportEvidence(string Kind, string NativeLibrary, string NativeEntryPoint);
}
