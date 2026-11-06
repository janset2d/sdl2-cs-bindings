using System.Text.RegularExpressions;

namespace Build.Tests.Fixtures.RepositoryGates;

internal sealed record MarkdownLink(int Line, string Target);

internal static partial class MarkdownLinks
{
    public static IReadOnlyList<MarkdownLink> Extract(string markdown)
    {
        var links = new List<MarkdownLink>();
        var fence = string.Empty;
        var lines = markdown.Split('\n');

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index].TrimEnd('\r');
            var marker = Fence().Match(line);

            if (marker.Success && (fence.Length != 0 || marker.Groups["fence"].Value[0] != '`'
                || !line[marker.Length..].Contains('`', StringComparison.Ordinal)))
            {
                var run = marker.Groups["fence"].Value;

                if (fence.Length == 0)
                {
                    fence = run;
                }
                else if (run[0] == fence[0] && run.Length >= fence.Length
                    && string.IsNullOrWhiteSpace(line[(marker.Index + marker.Length)..]))
                {
                    fence = string.Empty;
                }

                continue;
            }

            if (fence.Length != 0)
            {
                continue;
            }

            line = InlineCode().Replace(line, string.Empty);
            var definition = Reference().Match(line);

            if (definition.Success)
            {
                links.Add(new MarkdownLink(index + 1, definition.Groups["target"].Value));
            }

            foreach (Match match in InlineLink().Matches(line))
            {
                links.Add(new MarkdownLink(index + 1, match.Groups["target"].Value));
            }
        }

        return links;
    }

    public static string? ToRepositoryPath(string sourceFile, string target)
    {
        var github = GitHubLink().Match(target);

        if (github.Success)
        {
            target = "/" + target[github.Length..];
        }
        else if (target.StartsWith('#') || Scheme().IsMatch(target))
        {
            return null;
        }

        target = Uri.UnescapeDataString(target.Split(['#', '?'], StringSplitOptions.None)[0]).Replace('\\', '/');

        if (DrivePath().IsMatch(target))
        {
            return target;
        }

        var directoryEnd = sourceFile.LastIndexOf('/');
        var directory = directoryEnd < 0 ? string.Empty : sourceFile[..(directoryEnd + 1)];
        var combined = target.StartsWith('/') ? target[1..] : directory + target;
        var segments = new List<string>();

        foreach (var segment in combined.Split('/'))
        {
            if (segment is "" or ".")
            {
                continue;
            }

            if (segment == ".." && segments.Count > 0 && segments[^1] != "..")
            {
                segments.RemoveAt(segments.Count - 1);
            }
            else
            {
                segments.Add(segment);
            }
        }

        return segments.Count == 0 ? "." : string.Join('/', segments);
    }

    [GeneratedRegex(@"^\s{0,3}(?<fence>`{3,}|~{3,})", RegexOptions.ExplicitCapture, 1000)]
    private static partial Regex Fence();
    [GeneratedRegex(@"(?<ticks>`+)(?!`).*?(?<!`)\k<ticks>(?!`)", RegexOptions.ExplicitCapture, 1000)]
    private static partial Regex InlineCode();
    [GeneratedRegex(@"^\s{0,3}\[[^\]]+\]:\s*(?:<(?<target>[^>]+)>|(?<target>\S+))", RegexOptions.None, 1000)]
    private static partial Regex Reference();
    [GeneratedRegex(@"!?\[[^\]]*\]\(\s*(?:<(?<target>[^>]+)>|(?<target>(?:[^\s()]+|\([^()]*\))+))(?:\s+[^\r\n]*?)?\s*\)", RegexOptions.None, 1000)]
    private static partial Regex InlineLink();
    [GeneratedRegex(@"^https://github\.com/janset2d/sdl2-cs-bindings/(?:blob|tree)/master/", RegexOptions.None, 1000)]
    private static partial Regex GitHubLink();
    [GeneratedRegex(@"^[a-zA-Z]{2,}:", RegexOptions.None, 1000)]
    private static partial Regex Scheme();
    [GeneratedRegex(@"^[a-zA-Z]:/", RegexOptions.None, 1000)]
    private static partial Regex DrivePath();
}
