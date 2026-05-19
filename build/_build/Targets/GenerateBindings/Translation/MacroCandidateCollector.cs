using Build.Targets.GenerateBindings.Parsing;

namespace Build.Targets.GenerateBindings.Translation;

internal static class MacroCandidateCollector
{
    public static IReadOnlyList<MacroConstantCandidate> Collect(IReadOnlyList<CppAstParseResult> parseResults)
    {
        ArgumentNullException.ThrowIfNull(parseResults);

        var candidates = new List<MacroConstantCandidate>();
        foreach (var result in parseResults)
        {
            foreach (var compilation in result.Compilations)
            {
                foreach (var macro in compilation.Macros)
                {
                    var sourceFile = macro.SourceFile ?? string.Empty;
                    candidates.Add(new MacroConstantCandidate(
                        macro.Name,
                        macro.Value,
                        macro.Parameters ?? [],
                        macro.Tokens ?? [],
                        sourceFile,
                        Path.GetFileName(sourceFile),
                        result.ParseView.Name));
                }
            }
        }

        return candidates
            .OrderBy(candidate => candidate.ParseViewName, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.SourceFile, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.Name, StringComparer.Ordinal)
            .ToList();
    }
}
