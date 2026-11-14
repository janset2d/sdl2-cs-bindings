using System.Globalization;
using System.Text;
using Build.Results;

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
        AppendAudits(builder, family);
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
        var sdl2Cs = NativeImportNames(family.Sdl2Cs.Evidence);
        var dynapi = family.Dynapi.Exports;
        var symbols = generated.Concat(sdl2Cs).Concat(dynapi).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

        builder.AppendLine("### Function Matrix");
        builder.AppendLine();
        builder.AppendLine("| Function | Generated | SDL2-CS | Dynapi |");
        builder.AppendLine("| --- | --- | --- | --- |");

        foreach (var symbol in symbols.Take(SymbolLimit))
        {
            builder.Append(CultureInfo.CurrentCulture, $"| `{EscapeInline(symbol)}` | {YesNo(generated.Contains(symbol))} | ");
            builder.Append(CultureInfo.CurrentCulture, $"{YesNoOrNa(family.Sdl2Cs.Status, sdl2Cs.Contains(symbol))} | ");
            builder.AppendLine(CultureInfo.CurrentCulture, $"{YesNoOrNa(family.Dynapi.Status, dynapi.Contains(symbol))} |");
        }

        if (symbols.Length > SymbolLimit)
        {
            builder.AppendLine(CultureInfo.CurrentCulture, $"| ... | {symbols.Length - SymbolLimit} additional sorted function(s) omitted for readability. |  |  |");
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

    private static void AppendAudits(StringBuilder builder, FamilyReport family)
    {
        if (family.Config.UsesSdl2Dynapi)
        {
            builder.AppendLine("### Dynapi coherence");
            builder.AppendLine();
            builder.AppendLine("| Profile | Errors | Warnings |");
            builder.AppendLine("| --- | ---: | ---: |");
            AppendCoherenceRow(builder, "Stage1Generator", family.DynapiStage1);
            AppendCoherenceRow(builder, "Stage2Strict", family.DynapiStrict);
            builder.AppendLine();
            AppendCoherenceFindings(builder, "Stage1Generator", family.DynapiStage1);
            AppendCoherenceFindings(builder, "Stage2Strict", family.DynapiStrict);
        }

        if (family.Ownership is { } ownership)
        {
            builder.AppendLine("### Ownership audit");
            builder.AppendLine();
            builder.AppendLine("An uncovered symbol shows a prefix-roster gap, not proof of foreign ownership.");
            builder.AppendLine();

            if (family.OwnershipStatus != SourceStatus.Present)
            {
                builder.AppendLine("Evidence Missing: generated family tree unavailable.");
            }
            else if (ownership.Count == 0)
            {
                builder.AppendLine("All audited symbols are covered by owned_prefixes.");
            }

            foreach (var check in ownership)
            {
                builder.AppendLine(CultureInfo.CurrentCulture, $"- Warning: `{EscapeInline(check.Name)}`: {check.Message}.");
            }

            builder.AppendLine();
        }

        if (family.RequiredAudit is { } required)
        {
            builder.AppendLine("### Required-surface audit");
            builder.AppendLine();
            builder.AppendLine("Managed types are compared; NativeTypeName annotations are evidence, not comparison keys.");
            builder.AppendLine();
            builder.AppendLine("| Symbol | Backend | Result | Evidence |");
            builder.AppendLine("| --- | --- | --- | --- |");

            foreach (var row in required.Results)
            {
                builder.AppendLine(CultureInfo.CurrentCulture, $"| {EscapeText(row.Symbol)} | {row.Backend} | {row.Status} | {EscapeText(row.Evidence)} |");
            }

            builder.AppendLine();
        }
    }

    private static void AppendCoherenceRow(StringBuilder builder, string name, ValidationReport? report)
    {
        var errors = report?.Errors.Count.ToString(CultureInfo.CurrentCulture) ?? "Evidence Missing";
        var warnings = report?.Warnings.Count.ToString(CultureInfo.CurrentCulture) ?? "Evidence Missing";
        builder.AppendLine(CultureInfo.CurrentCulture, $"| {name} | {errors} | {warnings} |");
    }

    private static void AppendCoherenceFindings(StringBuilder builder, string name, ValidationReport? report)
    {
        if (report is null || report.Count == 0)
        {
            return;
        }

        builder.AppendLine(CultureInfo.CurrentCulture, $"#### {name}");
        builder.AppendLine();

        foreach (var check in report)
        {
            builder.AppendLine(CultureInfo.CurrentCulture, $"- {check.Severity}: {EscapeText(check.Name)}: {EscapeText(check.Message)}");
        }

        builder.AppendLine();
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
