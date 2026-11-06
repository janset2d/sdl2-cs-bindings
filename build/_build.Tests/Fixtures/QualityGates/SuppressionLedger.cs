using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Build.Tests.Fixtures.RepositoryGates;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Build.Tests.Fixtures.QualityGates;

internal static partial class SuppressionLedger
{
    public const string BaselinePath = "build/_build.Tests/Integration/QualityGates/suppression-ledger.baseline";

    private static readonly string[] Channels =
    [
        "pragma", "nullable", "suppress-message", "slopwatch-suppress", "slopwatch-ignore", "slopwatch-baseline", "editorconfig", "globalconfig",
        "no-warn", "warnings-not-as-errors", "msbuild-warnings-as-messages", "generated-code-header", "generated-code-attribute", "build-switch",
        "no-error", "staging-marker",
    ];

    private static readonly string[] BuildSwitches =
    [
        "TreatWarningsAsErrors", "MSBuildTreatWarningsAsErrors", "CodeAnalysisTreatWarningsAsErrors", "WarningsAsErrors", "EnableNETAnalyzers",
        "EnforceCodeStyleInBuild", "WarningLevel", "Nullable", "CodeAnalysisRuleSet", "IsAotCompatible", "IsTrimmable", "EnableTrimAnalyzer",
        "EnableAotAnalyzer", "EnableSingleFileAnalyzer", "EnableSourceControlManagerQueries", "EnableSourceLink",
    ];

    public static IReadOnlyList<string> ReadTracked() => Read(RepositoryFiles.Tracked
        .Where(path => !RepositoryFiles.Submodules.Contains(path) && IsScanned(path))
        .Select(path => (path, RepositoryFiles.ReadText(path) ?? throw new FileNotFoundException(path))));

    public static IReadOnlyList<string> Read(IEnumerable<(string Path, string Content)> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var entries = new List<string>();
        var generatedDirectories = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (path, content) in files)
        {
            if (path.EndsWith(".cs", StringComparison.Ordinal))
            {
                ReadCode(path, content, entries);
                ReadGeneratedHeader(path, content, entries, generatedDirectories);
                ReadFileProperties(path, content, entries);
            }
            else if (path.EndsWith(".editorconfig", StringComparison.Ordinal) || path.EndsWith(".globalconfig", StringComparison.Ordinal))
            {
                ReadConfig(path, content, entries);
            }
            else if (path.EndsWith(".csproj", StringComparison.Ordinal) || path.EndsWith(".props", StringComparison.Ordinal)
                || path.EndsWith(".targets", StringComparison.Ordinal) || path.EndsWith(".ruleset", StringComparison.OrdinalIgnoreCase))
            {
                ReadProject(path, content, entries);
            }
            else if (path == ".slopwatch/baseline.json")
            {
                ReadSlopwatchBaseline(path, content, entries);
            }
            else
            {
                ReadCommandLine(path, content, entries);
            }
        }

        return [.. entries.Order(StringComparer.Ordinal)];
    }

    public static string Render(IReadOnlyList<string> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var text = new StringBuilder("Suppression ledger: every diagnostic escape is counted.\n\n");

        foreach (var channel in Channels.Order(StringComparer.Ordinal))
        {
            text.Append(channel).Append(": ").Append(entries.Count(entry => entry.StartsWith(channel + " ", StringComparison.Ordinal))).Append('\n');
        }

        text.Append('\n');

        foreach (var group in entries.GroupBy(entry => entry, StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            text.Append(group.Key);

            if (group.Count() > 1)
            {
                text.Append(" x").Append(group.Count());
            }

            text.Append('\n');
        }

        return text.ToString();
    }

    public static IEnumerable<string> BaselineEntries(string text) => text
        .ReplaceLineEndings("\n")
        .Split('\n')
        .Where(line => Channels.Any(channel => line.StartsWith(channel + " ", StringComparison.Ordinal)));

    public static IEnumerable<string> StagingMarkers(string text) => text
        .ReplaceLineEndings("\n")
        .Split('\n')
        .Where(line => line.StartsWith("# QM-", StringComparison.Ordinal));

    private static bool IsScanned(string path) => path.EndsWith(".cs", StringComparison.Ordinal)
        || path.EndsWith(".csproj", StringComparison.Ordinal) || path.EndsWith(".props", StringComparison.Ordinal) || path.EndsWith(".targets", StringComparison.Ordinal)
        || path.EndsWith(".editorconfig", StringComparison.Ordinal) || path.EndsWith(".globalconfig", StringComparison.Ordinal)
        || path == ".slopwatch/baseline.json" || path.EndsWith(".ruleset", StringComparison.OrdinalIgnoreCase)
        || (path.StartsWith(".github/", StringComparison.Ordinal) && (path.EndsWith(".yml", StringComparison.Ordinal) || path.EndsWith(".yaml", StringComparison.Ordinal)));

    private static string Entry(string channel, string path, string scope, string rule, string value = "") =>
        $"{channel} {path} [{scope}] {rule}" + (value.Length == 0 ? "" : " = " + value);

    private static void ReadCode(string path, string content, List<string> entries)
    {
        var root = CSharpSyntaxTree.ParseText(content).GetCompilationUnitRoot();
        var aliases = root.Usings
            .Where(directive => directive.Alias is not null && directive.Name is not null)
            .ToDictionary(directive => directive.Alias!.Name.Identifier.ValueText, directive => SimpleName(directive.Name!), StringComparer.Ordinal);

        foreach (var node in root.DescendantNodes(descendIntoTrivia: true))
        {
            if (node is PragmaWarningDirectiveTriviaSyntax pragma && pragma.DisableOrRestoreKeyword.IsKind(SyntaxKind.DisableKeyword))
            {
                foreach (var rule in pragma.ErrorCodes.Select(code => code.ToString()).DefaultIfEmpty("*"))
                {
                    entries.Add(Entry("pragma", path, "", rule));
                }
            }
            else if (node is NullableDirectiveTriviaSyntax nullable && nullable.SettingToken.IsKind(SyntaxKind.DisableKeyword))
            {
                entries.Add(Entry("nullable", path, "", $"disable {nullable.TargetToken.ValueText}".Trim()));
            }
            else if (node is AttributeSyntax attribute)
            {
                ReadAttribute(path, attribute, aliases, entries);
            }
        }

        foreach (var trivia in root.DescendantTrivia(descendIntoTrivia: true))
        {
            if (trivia.IsKind(SyntaxKind.DisabledTextTrivia))
            {
                ReadCode(path, trivia.ToFullString(), entries);
            }
            else if (trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia))
            {
                ReadIgnores(path, trivia.ToFullString(), entries);
            }
        }
    }

    private static void ReadAttribute(string path, AttributeSyntax attribute, Dictionary<string, string> aliases, List<string> entries)
    {
        var name = SimpleName(attribute.Name);
        name = aliases.GetValueOrDefault(name, name);
        name = name.EndsWith("Attribute", StringComparison.Ordinal) ? name[..^9] : name;

        if (name == "GeneratedCode")
        {
            entries.Add(Entry("generated-code-attribute", path, "", name));
        }
        else if (name is "SuppressMessage" or "UnconditionalSuppressMessage" or "SlopwatchSuppress")
        {
            var position = name == "SlopwatchSuppress" ? 0 : 1;
            var parameter = name == "SlopwatchSuppress" ? "ruleId" : "checkId";
            var arguments = attribute.ArgumentList?.Arguments ?? default;
            var argument = arguments.FirstOrDefault(item => item.NameColon?.Name.Identifier.ValueText == parameter)
                ?? arguments.Where(item => item.NameColon is null && item.NameEquals is null).ElementAtOrDefault(position);

            if (argument?.Expression is not LiteralExpressionSyntax literal || !literal.IsKind(SyntaxKind.StringLiteralExpression))
            {
                throw new InvalidOperationException(path + ": suppression rule must be a string literal: " + attribute.ToString());
            }

            var channel = name == "SlopwatchSuppress" ? "slopwatch-suppress" : "suppress-message";
            var scope = (attribute.Parent as AttributeListSyntax)?.Target?.Identifier.ValueText ?? "";
            entries.Add(Entry(channel, path, scope, literal.Token.ValueText.Split(':')[0]));
        }
    }

    private static string SimpleName(NameSyntax name) => name switch
    {
        QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
        AliasQualifiedNameSyntax alias => alias.Name.Identifier.ValueText,
        SimpleNameSyntax simple => simple.Identifier.ValueText,
        _ => "",
    };

    private static void ReadGeneratedHeader(string path, string content, List<string> entries, HashSet<string> directories)
    {
        var marker = path.IndexOf("/Generated/", StringComparison.Ordinal);
        var root = CSharpSyntaxTree.ParseText(content).GetCompilationUnitRoot();

        if (!(marker >= 0 && path.EndsWith(".g.cs", StringComparison.Ordinal))
            && !root.GetLeadingTrivia().Any(trivia => trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)
                && trivia.ToString().StartsWith("// <auto-generated", StringComparison.Ordinal)))
        {
            return;
        }

        var origin = marker < 0 ? path : path[..(marker + 11)] + path[(marker + 11)..].Split('/')[0];

        if (directories.Add(origin))
        {
            entries.Add(Entry("generated-code-header", origin, "", "<auto-generated>"));
        }
    }

    private static void ReadConfig(string path, string content, List<string> entries)
    {
        var channel = path.EndsWith(".globalconfig", StringComparison.Ordinal) || content.Contains("is_global", StringComparison.Ordinal) ? "globalconfig" : "editorconfig";
        var scope = "";
        var comments = new List<string>();
        var sectionComments = "";

        foreach (var raw in content.ReplaceLineEndings("\n").Split('\n'))
        {
            var line = raw.Trim();

            if (line.StartsWith("# QM-", StringComparison.Ordinal))
            {
                entries.Add(Entry("staging-marker", path, "", line));
            }

            if (line.StartsWith('#') || line.StartsWith(';'))
            {
                comments.Add(line);
                continue;
            }

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                scope = line[1..^1];
                sectionComments = string.Join(' ', comments);
                comments.Clear();
                continue;
            }

            var separator = line.IndexOfAny(['=', ':']);

            if (separator >= 0)
            {
                var key = line[..separator].Trim();
                var value = line[(separator + 1)..].Trim();

                if (LowersDiagnostic(key, value))
                {
                    var reason = comments.Count > 0 ? string.Join(' ', comments) : sectionComments;
                    entries.Add(Entry(channel, path, scope, key, value) + (reason.Length > 0 ? " | " + reason : ""));
                }

                comments.Clear();
            }
        }
    }

    private static bool LowersDiagnostic(string key, string value) =>
        (key.EndsWith(".severity", StringComparison.OrdinalIgnoreCase) && value.ToLowerInvariant() is "none" or "suggestion" or "silent" or "refactoring")
        || value.EndsWith(":suggestion", StringComparison.OrdinalIgnoreCase) || value.EndsWith(":none", StringComparison.OrdinalIgnoreCase)
        || (key == "generated_code" && value == "true") || (key == "roslynator_analyzers.enabled_by_default" && value == "false")
        || RuleOption().IsMatch(key);

    private static void ReadProject(string path, string content, List<string> entries)
    {
        if (path.EndsWith(".ruleset", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"{path}: rule sets are not an inventoried suppression channel.");
        }

        var document = XDocument.Parse(content);

        foreach (var property in document.Descendants().Where(element => element.Parent?.Name.LocalName == "PropertyGroup"))
        {
            var scope = string.Join(" and ", property
                .AncestorsAndSelf()
                .Reverse()
                .Select(element => (string?)element.Attribute("Condition"))
                .OfType<string>());
            ReadProperty(path, property.Name.LocalName, property.Value, scope, entries);
        }

        foreach (var import in document.Descendants("Import").Where(element => element.Attribute("Condition") is not null))
        {
            entries.Add(Entry("build-switch", path, (string?)import.Attribute("Condition") ?? "", "Import", (string?)import.Attribute("Project") ?? ""));
        }

        foreach (var comment in document.DescendantNodes().OfType<XComment>())
        {
            ReadIgnores(path, comment.Value, entries);
        }
    }

    private static void ReadProperty(string path, string name, string value, string scope, List<string> entries)
    {
        var channel = name.ToUpperInvariant() switch
        {
            "NOWARN" => "no-warn",
            "NOERROR" => "no-error",
            "WARNINGSNOTASERRORS" => "warnings-not-as-errors",
            "MSBUILDWARNINGSASMESSAGES" => "msbuild-warnings-as-messages",
            _ => null,
        };

        if (channel is not null)
        {
            foreach (var rule in value.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (string.Equals(rule, "$(" + name + ")", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (rule.StartsWith("$(", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"{path}: {name} takes hidden IDs from {rule}.");
                }

                entries.Add(Entry(channel, path, scope, rule));
            }
        }
        else if (BuildSwitches.Contains(name, StringComparer.OrdinalIgnoreCase) || name.StartsWith("RunAnalyzers", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("AnalysisLevel", StringComparison.OrdinalIgnoreCase) || name.StartsWith("AnalysisMode", StringComparison.OrdinalIgnoreCase))
        {
            entries.Add(Entry("build-switch", path, scope, name, value.Trim()));
        }
    }

    private static void ReadCommandLine(string path, string content, List<string> entries)
    {
        foreach (Match match in CommandProperty().Matches(content))
        {
            ReadProperty(path, match.Groups["name"].Value, match.Groups["value"].Value.Trim('"', '\''), "command line", entries);
        }
    }

    private static void ReadFileProperties(string path, string content, List<string> entries)
    {
        foreach (Match match in FileProperty().Matches(content))
        {
            ReadProperty(path, match.Groups["name"].Value, match.Groups["value"].Value, "file-based header", entries);
        }
    }

    private static void ReadIgnores(string path, string text, List<string> entries)
    {
        foreach (Match match in SlopwatchIgnore().Matches(text))
        {
            entries.Add(Entry("slopwatch-ignore", path, "", match.Groups["rule"].Value, match.Groups["reason"].Value.Trim().TrimEnd('-', '>').Trim()));
        }
    }

    private static void ReadSlopwatchBaseline(string path, string content, List<string> entries)
    {
        using var document = JsonDocument.Parse(content);

        foreach (var entry in document.RootElement.GetProperty("entries").EnumerateArray())
        {
            entries.Add(Entry("slopwatch-baseline", path, entry.GetProperty("filePath").GetString()!.Replace('\\', '/'),
                entry.GetProperty("ruleId").GetString()!, entry.GetProperty("hash").GetString()!));
        }
    }

    [GeneratedRegex(@"^(?:[A-Za-z]{1,8}[0-9]{3,5}|dotnet_code_quality)[._]", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex RuleOption();

    [GeneratedRegex(@"(?:^|\s)(?:-p:|/p:|--property:|-property:)(?<name>[A-Za-z]+)=(?<value>\$\{\{[^}]*\}\}|""[^""]*""|'[^']*'|\S+)",
        RegexOptions.Multiline | RegexOptions.CultureInvariant, 1000)]
    private static partial Regex CommandProperty();

    [GeneratedRegex(@"^#:property\s+(?<name>[A-Za-z]+)\s*=(?<value>.*)$", RegexOptions.Multiline | RegexOptions.CultureInvariant, 1000)]
    private static partial Regex FileProperty();

    [GeneratedRegex(@"slopwatch-ignore(?:-start)?\s*:\s*(?<rule>SW\d{3})\s*(?<reason>[^\r\n]*)", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex SlopwatchIgnore();
}
