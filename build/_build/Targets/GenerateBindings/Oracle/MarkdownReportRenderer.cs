using System.Globalization;
using System.Text;

namespace Build.Targets.GenerateBindings.Oracle;

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
        builder.AppendLine(CultureInfo.CurrentCulture, $"## Family: {family.Config.FamilyId}");
        builder.AppendLine();
        builder.AppendLine(CultureInfo.CurrentCulture, $"- Display name: {family.Config.DisplayName}");
        builder.AppendLine(CultureInfo.CurrentCulture, $"- Expected namespace: `{family.Config.ExpectedNamespace}`");
        builder.AppendLine(CultureInfo.CurrentCulture, $"- Expected raw class: `{family.Config.ExpectedRawClassName}`");
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
        builder.AppendLine(CultureInfo.CurrentCulture,
            $"| Generated ClangSharp | {generated.Functions.Count} | {generated.Constants.Count} | {generated.Types.Count} |");
        builder.AppendLine(CultureInfo.CurrentCulture,
            $"| Cake Preview | {family.CakePreview.Evidence.Functions.Count} | {family.CakePreview.Evidence.Constants.Count} | {family.CakePreview.Evidence.Types.Count} |");
        builder.AppendLine(CultureInfo.CurrentCulture,
            $"| SDL2-CS | {family.Sdl2Cs.Evidence.Functions.Count} | {family.Sdl2Cs.Evidence.Constants.Count} | {family.Sdl2Cs.Evidence.Types.Count} |");
        builder.AppendLine(CultureInfo.CurrentCulture, $"| Dynapi | {family.Dynapi.Exports.Count} | 0 | 0 |");
        builder.AppendLine(CultureInfo.CurrentCulture,
            $"| Manifest Required Surface | {family.RequiredSurface.RequiredFunctions.Count} | {family.RequiredSurface.RequiredConstants.Count} | 0 |");
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

        foreach (var classificationGroup in checks.GroupBy(check => check.Classification, StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            builder.AppendLine(CultureInfo.CurrentCulture, $"#### {classificationGroup.Key}");
            builder.AppendLine();

            foreach (var checkGroup in classificationGroup
                .GroupBy(check => check.CheckId, StringComparer.Ordinal)
                .OrderBy(group => RawAbiCheckTitle(group.Key), StringComparer.Ordinal))
            {
                var symbols = checkGroup.Select(check => check.Symbol).Where(symbol => symbol.Length > 0).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
                builder.AppendLine(CultureInfo.CurrentCulture,
                    $"- {RawAbiCheckTitle(checkGroup.Key)} (`{checkGroup.Key}`): {checkGroup.Count()} finding(s), {symbols.Length} symbol(s).");
                AppendBoundedSymbols(builder, symbols);

                foreach (var example in checkGroup.OrderBy(check => check.Symbol, StringComparer.Ordinal).ThenBy(check => check.SourcePath, StringComparer.Ordinal).Take(3))
                {
                    builder.Append(CultureInfo.CurrentCulture, $"  - Example: `{EscapeInline(example.Symbol)}` - {EscapeText(example.Message)} ");
                    builder.AppendLine(CultureInfo.CurrentCulture, $"(`{EscapeInline(FormatReportPath(repoRoot, example.SourcePath))}`)");
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
            builder.Append(CultureInfo.CurrentCulture, $"| `{EscapeInline(symbol)}` | {YesNo(generated.Contains(symbol))} | ");
            builder.Append(CultureInfo.CurrentCulture, $"{YesNoOrNa(family.CakePreview.Status, cake.Contains(symbol))} | ");
            builder.Append(CultureInfo.CurrentCulture, $"{YesNoOrNa(family.Sdl2Cs.Status, sdl2Cs.Contains(symbol))} | ");
            builder.AppendLine(CultureInfo.CurrentCulture, $"{YesNoOrNa(family.Dynapi.Status, dynapi.Contains(symbol))} |");
        }

        if (symbols.Length > SymbolLimit)
        {
            builder.AppendLine(CultureInfo.CurrentCulture, $"| ... | {symbols.Length - SymbolLimit} additional sorted function(s) omitted for readability. |  |  |  |");
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

        foreach (var check in family.RawAbiChecks
            .Where(check => check.CheckId is "required-function-missing" or "required-constant-missing")
            .OrderBy(check => check.CheckId, StringComparer.Ordinal)
            .ThenBy(check => check.Symbol, StringComparer.Ordinal))
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
            builder.Append("- ").AppendLine(gap);
        }

        builder.AppendLine();
    }

    private static void AppendBoundedSymbols(StringBuilder builder, string[] symbols)
    {
        if (symbols.Length == 0)
        {
            return;
        }

        builder.AppendLine("  - Symbols:");

        foreach (var symbol in symbols.Take(SymbolLimit))
        {
            builder.AppendLine(CultureInfo.CurrentCulture, $"  - `{EscapeInline(symbol)}`");
        }

        if (symbols.Length > SymbolLimit)
        {
            builder.AppendLine(CultureInfo.CurrentCulture, $"  - ... {symbols.Length - SymbolLimit} remaining symbol(s).");
        }
    }

    private static void AppendInputSourceRow(StringBuilder builder, string repoRoot, string familyId, SourceReport source)
    {
        builder.Append(CultureInfo.CurrentCulture, $"| {familyId} | {source.Name} | `{EscapeInline(FormatReportPath(repoRoot, source.Path))}` | ");
        builder.AppendLine(CultureInfo.CurrentCulture,
            $"{FormatStatus(source.Status)} | {source.FunctionCount} | {source.ConstantCount} | {source.TypeCount} |");
    }

    private static void AppendFamilySourceRow(StringBuilder builder, string repoRoot, SourceReport source)
    {
        builder.Append(CultureInfo.CurrentCulture, $"| {source.Name} | `{EscapeInline(FormatReportPath(repoRoot, source.Path))}` | ");
        builder.AppendLine(CultureInfo.CurrentCulture,
            $"{FormatStatus(source.Status)} | {source.FunctionCount} | {source.ConstantCount} | {source.TypeCount} |");
    }

    private static HashSet<string> NativeImportNames(CSharpEvidence evidence)
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
        => text.Replace('`', '\'').Replace("|", "\\|", StringComparison.Ordinal);

    private static string EscapeText(string text)
        => text.Replace("|", "\\|", StringComparison.Ordinal);
}
