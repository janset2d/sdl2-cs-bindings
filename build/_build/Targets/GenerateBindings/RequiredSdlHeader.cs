using System.Text;
using System.Text.RegularExpressions;
using Build.Data.BindingGeneration.Models;
using Build.Results;

namespace Build.Targets.GenerateBindings;

internal static partial class RequiredSdlHeader
{
    public static Result<string, string> Render(string source, BindingGenerationConfig family)
    {
        var functionNames = family.RequiredFunctions.Where(function => function.SourceHeader == "SDL.h").Select(function => function.Name).ToArray();
        var constantNames = family.RequiredConstants.Where(constant => constant.SourceHeader == "SDL.h").Select(constant => constant.Name).ToArray();
        var functions = ClangSharpOutputClassifier
            .Lines()
            .Split(source)
            .Select(line => Function().Match(line.Trim()))
            .Where(match => match.Success)
            .ToDictionary(match => match.Groups["name"].Value, StringComparer.Ordinal);
        var functionError = NamesError(functions.Keys, functionNames, "direct functions", "functions");

        if (functionError is not null)
        {
            return Result<string, string>.Failure(functionError);
        }

        var renderedFunctions = new List<string>();

        foreach (var name in functionNames)
        {
            var rendered = RenderFunction(functions[name]);

            if (rendered.TryGetError(out var error))
            {
                return Result<string, string>.Failure(error);
            }

            renderedFunctions.Add(rendered.Value);
        }

        var constants = LogicalLines(source)
            .Select(line => Macro().Match(line.Trim()))
            .Where(match => match.Success)
            .ToDictionary(match => match.Groups["name"].Value, match => NormalizeMacro(match.Groups["value"].Value), StringComparer.Ordinal);
        var constantError = NamesError(constants.Keys, constantNames, "SDL_INIT macros", "SDL_INIT macros");

        if (constantError is not null)
        {
            return Result<string, string>.Failure(constantError);
        }

        var lines = new List<string>
        {
            "using System.Runtime.InteropServices;", "", "namespace " + family.ManagedNamespace, "{",
            "    internal static unsafe partial class " + family.RawClassName, "    {",
        };

        foreach (var name in constantNames)
        {
            lines.Add($"        [NativeTypeName(\"#define {name} {constants[name]}\")]");
            lines.Add($"        public const uint {name} = {constants[name]};");
            lines.Add("");
        }

        lines.Add(string.Join("\n\n", renderedFunctions));
        lines.AddRange(["    }", "}", ""]);
        return Result<string, string>.Success(string.Join('\n', lines));
    }

    private static Result<string, string> RenderFunction(Match function)
    {
        var parameters = new List<string>();
        var parameterText = function.Groups["parameters"].Value.Trim();

        if (parameterText is not "void" and not "")
        {
            foreach (var parameter in parameterText.Split(','))
            {
                var parts = Whitespace().Split(parameter.Trim());

                if (parts.Length != 2)
                {
                    return Result<string, string>.Failure("Unsupported SDL.h parameter declaration: " + parameter);
                }

                var type = MapType(parts[0]);
                var prefix = parts[0] == type ? "" : $"[NativeTypeName(\"{parts[0]}\")] ";
                parameters.Add(prefix + type + " " + parts[1]);
            }
        }

        var nativeReturn = function.Groups["return_type"].Value;
        var managedReturn = MapType(nativeReturn);
        var result = "        [DllImport(\"SDL2\", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]\n";

        if (nativeReturn != managedReturn)
        {
            result += $"        [return: NativeTypeName(\"{nativeReturn}\")]\n";
        }

        result += $"        public static extern {managedReturn} {function.Groups["name"].Value}({string.Join(", ", parameters)});";
        return Result<string, string>.Success(result);
    }

    private static string MapType(string nativeType) => nativeType == "Uint32" ? "uint" : nativeType;

    private static string? NamesError(IEnumerable<string> actual, IReadOnlyList<string> allowed, string extraLabel, string missingLabel)
    {
        var discovered = actual.ToHashSet(StringComparer.Ordinal);
        var extra = discovered.Except(allowed, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

        if (extra.Length > 0)
        {
            return $"SDL.h declares unexpected {extraLabel}: {string.Join(", ", extra)}";
        }

        var missing = allowed.Where(name => !discovered.Contains(name)).ToArray();
        return missing.Length == 0 ? null : $"SDL.h is missing required {missingLabel}: {string.Join(", ", missing)}";
    }

    private static IEnumerable<string> LogicalLines(string source)
    {
        var pending = new StringBuilder();

        foreach (var line in ClangSharpOutputClassifier.Lines().Split(source).Select(line => line.TrimEnd()))
        {
            if (line.EndsWith('\\', StringComparison.Ordinal))
            {
                pending.Append(line[..^1].Trim()).Append(' ');
                continue;
            }

            yield return pending.Length == 0 ? line : pending + line.Trim();
            pending.Clear();
        }

        if (pending.Length > 0)
        {
            yield return pending.ToString().Trim();
        }
    }

    private static string NormalizeMacro(string value)
    {
        var normalized = Whitespace().Replace(Comment().Replace(value, "").Trim(), " ");

        if (normalized.StartsWith('(', StringComparison.Ordinal) && normalized.EndsWith(')', StringComparison.Ordinal))
        {
            normalized = normalized[1..^1].Trim();
        }

        return HexSuffix().Replace(normalized, "${hex}U");
    }

    [GeneratedRegex(@"^extern\s+DECLSPEC\s+(?<return_type>.+?)\s+SDLCALL\s+(?<name>SDL_\w+)\((?<parameters>.*?)\);$", RegexOptions.CultureInvariant, 500)]
    private static partial Regex Function();
    [GeneratedRegex(@"^#define\s+(?<name>SDL_INIT_\w+)\s+(?<value>.+)$", RegexOptions.CultureInvariant, 500)]
    private static partial Regex Macro();
    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.CultureInvariant, 500)]
    private static partial Regex Comment();
    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant, 500)]
    private static partial Regex Whitespace();
    [GeneratedRegex(@"\b(?<hex>0x[0-9A-Fa-f]+)u\b", RegexOptions.CultureInvariant, 500)]
    private static partial Regex HexSuffix();
}
