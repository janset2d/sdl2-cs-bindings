using System.Text.RegularExpressions;

namespace Build.Targets.GenerateBindings;

internal static partial class NeutralSymbolExclusions
{
    public static IReadOnlyList<string> Extract(string source)
        => [.. Declarations().Matches(source).Select(match => match.Groups["name"].Value).Distinct(StringComparer.Ordinal)];

    [GeneratedRegex(@"^\s*(?:\[[^\]]+\]\s*)*public[^(;]*?\b(?<name>SDL_\w+)\s*[({=;]", RegexOptions.Multiline | RegexOptions.CultureInvariant, 500)]
    private static partial Regex Declarations();
}
