using System.Globalization;
using System.Text;

namespace Janset.Sdl2.AlimerSpike.Generator;

internal sealed record FamilyStats(
    int Headers,
    int FunctionsSeen,
    int FunctionsEmitted,
    int FunctionsSkipped);

internal sealed record SkipEntry(
    string Family,
    string Header,
    string FunctionName,
    SkipReason Reason,
    string Detail);

internal static class Sdl2GenerationReport
{
    public static void Write(
        Sdl2SpikeOptions options,
        IReadOnlyDictionary<string, FamilyStats> stats,
        IReadOnlyList<SkipEntry> skips,
        IReadOnlyList<string> parseDiagnostics)
    {
        Directory.CreateDirectory(options.ReportsRoot);
        var reportPath = Path.Combine(options.ReportsRoot, options.Scope == SpikeScope.Bootstrap ? "alimer-bootstrap.md" : "alimer-full.md");

        var sb = new StringBuilder();
        sb.AppendLine($"# Alimer-Style CppAst {options.Scope} Report");
        sb.AppendLine();
        sb.AppendLine($"**Triplet:** {options.VcpkgTriplet}");
        sb.AppendLine($"**Mode:** {(options.Emit ? "emit" : "parse-only")}");
        sb.AppendLine();
        sb.AppendLine("| Family | Headers | Functions Seen | Functions Emitted | Functions Skipped |");
        sb.AppendLine("| --- | ---: | ---: | ---: | ---: |");
        foreach (var (family, s) in stats)
        {
            sb.AppendLine($"| {family} | {s.Headers} | {s.FunctionsSeen} | {s.FunctionsEmitted} | {s.FunctionsSkipped} |");
        }
        sb.AppendLine();

        sb.AppendLine("## Skips");
        sb.AppendLine();
        if (skips.Count == 0)
        {
            sb.AppendLine("No skips recorded.");
        }
        else
        {
            var grouped = skips.GroupBy(s => s.Reason).OrderByDescending(g => g.Count());
            foreach (var group in grouped)
            {
                sb.AppendLine($"### {group.Key} ({group.Count()})");
                sb.AppendLine();
                sb.AppendLine("| Family | Header | Function | Detail |");
                sb.AppendLine("| --- | --- | --- | --- |");
                foreach (var entry in group.OrderBy(e => e.FunctionName, StringComparer.Ordinal).Take(40))
                {
                    sb.AppendLine($"| {entry.Family} | {entry.Header} | `{entry.FunctionName}` | {entry.Detail} |");
                }
                if (group.Count() > 40)
                {
                    sb.AppendLine($"| ... | ... | ... | ({group.Count() - 40} more) |");
                }
                sb.AppendLine();
            }
        }

        sb.AppendLine("## Parse Diagnostics");
        sb.AppendLine();
        if (parseDiagnostics.Count == 0)
        {
            sb.AppendLine("No diagnostics recorded.");
        }
        else
        {
            foreach (var diagnostic in parseDiagnostics.Take(40))
            {
                sb.AppendLine($"- {diagnostic}");
            }
            if (parseDiagnostics.Count > 40)
            {
                sb.AppendLine($"- ... ({parseDiagnostics.Count - 40} more)");
            }
        }

        File.WriteAllText(reportPath, sb.ToString(), Encoding.UTF8);
        Console.WriteLine($"Wrote {reportPath}");
    }
}
