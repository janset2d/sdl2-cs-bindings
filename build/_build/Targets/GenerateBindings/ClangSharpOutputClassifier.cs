using System.Text.RegularExpressions;

namespace Build.Targets.GenerateBindings;

internal static partial class ClangSharpOutputClassifier
{
    public static ClangSharpOutput Classify(int exitCode, bool exists, long length, string input, string output)
    {
        var macros = new List<string>();
        var diagnostics = new List<string>();
        var accepted = exitCode != 0 && exists && !output.Contains("fatal error:", StringComparison.Ordinal) &&
            !output.Contains($"Skipping '{input}' due to one or more errors listed above.", StringComparison.Ordinal) &&
            !(output.Contains("Parsing failed for 'declspec' due to 'CXError_Failure'", StringComparison.Ordinal) &&
                output.Contains("Skipping 'declspec' due to one or more errors listed above.", StringComparison.Ordinal));

        foreach (var line in Lines().Split(output).Select(line => line.Trim()).Where(line => line.Length > 0))
        {
            if (Framing().IsMatch(line))
            {
                continue;
            }

            if (line is "warning: redefining builtin macro [-Wbuiltin-macro-redefined]" or "warning: undefining builtin macro [-Wbuiltin-macro-redefined]")
            {
                diagnostics.Add(line);
                continue;
            }

            var match = MacroWarning().Match(line);

            if (!match.Success)
            {
                accepted = false;
                continue;
            }

            macros.Add(match.Groups["name"].Value);
        }

        accepted &= macros.Count > 0;
        var noOp = !accepted && exitCode == 0 && exists && length == 0;
        return new ClangSharpOutput(accepted, exitCode != 0 && !accepted, !accepted && !noOp && !(exists && length > 0), noOp,
            accepted ? macros : [], accepted ? diagnostics : []);
    }

    public static int ExitCode(bool clangSharpFailed, bool postprocessFailed, bool emptyOutput)
    {
        if (clangSharpFailed)
        {
            return 2;
        }

        if (postprocessFailed)
        {
            return 3;
        }

        return emptyOutput ? 4 : 0;
    }

    [GeneratedRegex(@"\r\n|[\n\r\v\f\x1c-\x1e\x85\u2028\u2029]", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 500)]
    internal static partial Regex Lines();

    [GeneratedRegex(@"^(?:Diagnostics for '[^']+':|Processing '[^']+'|Diagnostics for binding generation of .+:)$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 500)]
    private static partial Regex Framing();

    [GeneratedRegex(@"^Warning \([^)]*\bLine\b[^)]*,\s*\bColumn\b[^)]*\): Function like macro definition records are not supported: '(?<name>[^']+)'. " +
        @"Generated bindings may be incomplete\.$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 500)]
    private static partial Regex MacroWarning();
}
